using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Controller.Collections;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Tvdb.ScheduledTasks
{
    /// <summary>
    /// Scan for box sets task.
    /// </summary>
    public class ScanForBoxSetsTask : IScheduledTask
    {
        private readonly ILogger<ScanForBoxSetsTask> _logger;
        private readonly ICollectionManager _collectionManager;
        private readonly ILibraryManager _libraryManager;
        private readonly TvdbClientManager _tvdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScanForBoxSetsTask"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{ScanForBoxSetsTask}"/> interface.</param>
        /// <param name="collectionManager">Instance of <see cref="ICollectionManager"/>.</param>
        /// <param name="libraryManager">Instance of <see cref="ILibraryManager"/>.</param>
        /// <param name="tvdbClientManager">Instance of <see cref="TvdbClientManager"/>.</param>
        public ScanForBoxSetsTask(
            ILogger<ScanForBoxSetsTask> logger,
            ICollectionManager collectionManager,
            ILibraryManager libraryManager,
            TvdbClientManager tvdbClientManager)
        {
            _logger = logger;
            _collectionManager = collectionManager;
            _libraryManager = libraryManager;
            _tvdbClientManager = tvdbClientManager;
        }

        /// <inheritdoc />
        public string Name => "Scan library for new Tvdb box sets";

        /// <inheritdoc />
        public string Key => "TVDBBoxSetsRefreshLibraryTask";

        /// <inheritdoc />
        public string Description => "Scans all libraries for movies and series and adds them to box sets if the conditions are met.";

        /// <inheritdoc />
        public string Category => "TheTVDB";

        /// <inheritdoc/>
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting TMDbBoxSets refresh library task");
            await ScanForBoxSets(progress).ConfigureAwait(false);
            _logger.LogInformation("TMDbBoxSets refresh library task finished");
        }

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Enumerable.Empty<TaskTriggerInfo>();
        }

        private async Task ScanForBoxSets(IProgress<double> progress)
        {
            var items = GetItemsWithCollectionId();
            _logger.LogInformation("Found {0} items with tvdb collection id", items.Count);
            var boxSets = GetBoxSets();
            _logger.LogInformation("Found {0} box sets with tvdb id", boxSets.Count);

            var itemsByCollectionId = items.SelectMany(i =>
            {
                var collectionId = i.GetProviderId(TvdbPlugin.CollectionProviderId);
                return collectionId!.Split(';')
                .Where(c => !string.IsNullOrEmpty(c))
                .Select(c => new { CollectionId = c, Item = i });
            }).GroupBy(i => i.CollectionId)
            .ToList();
            int index = 0;
            foreach (var itemCollection in itemsByCollectionId)
            {
                progress.Report(100.0 * index / itemsByCollectionId.Count);
                var collectionId = itemCollection.Key;
                var boxSet = boxSets.FirstOrDefault(b => b.GetProviderId(TvdbPlugin.ProviderId) == collectionId);
                await AddItemToCollection(itemCollection.Select(i => i.Item).ToList(), collectionId, boxSet).ConfigureAwait(false);
                index++;
            }

            progress.Report(100);
        }

        private List<BaseItem> GetItemsWithCollectionId()
        {
            var items = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Series },
                IsVirtualItem = false,
                OrderBy = new List<ValueTuple<ItemSortBy, SortOrder>>
                {
                    new (ItemSortBy.Name, SortOrder.Ascending)
                },
                Recursive = true,
                HasTvdbId = true,
            });

            return items.Where(i => i.HasProviderId(TvdbPlugin.CollectionProviderId)
            && !string.IsNullOrEmpty(i.GetProviderId(TvdbPlugin.CollectionProviderId))).ToList();
        }

        private List<BoxSet> GetBoxSets()
        {
            return _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.BoxSet },
                CollapseBoxSetItems = false,
                Recursive = true,
                HasTvdbId = true,
            }).OfType<BoxSet>().ToList();
        }

        private async Task AddItemToCollection(IReadOnlyList<BaseItem> items, string collectionId, BoxSet? boxSet)
        {
            if (boxSet == null)
            {
                var collectionName = await GetBoxSetName(collectionId).ConfigureAwait(false);
                boxSet = await _collectionManager.CreateCollectionAsync(new CollectionCreationOptions
                {
                    Name = collectionName,
                    ProviderIds = new Dictionary<string, string>
                    {
                        { TvdbPlugin.ProviderId, collectionId },
                    },
                }).ConfigureAwait(false);
            }

            var itemIds = items
                .Where(i => !boxSet.ContainsLinkedChildByItemId(i.Id))
                .Select(i => i.Id)
                .ToList();

            if (items.Count == 0)
            {
                return;
            }

            await _collectionManager.AddToCollectionAsync(boxSet.Id, itemIds).ConfigureAwait(false);
        }

        private async Task<string> GetBoxSetName(string collectionId)
        {
            var collectionIdInt = int.Parse(collectionId, CultureInfo.InvariantCulture);
            var collection = await _tvdbClientManager.GetBoxSetExtendedByIdAsync(collectionIdInt, CancellationToken.None).ConfigureAwait(false);
            return collection.Name;
        }
    }
}
