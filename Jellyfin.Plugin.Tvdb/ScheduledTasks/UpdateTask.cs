using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;
using Tvdb.Sdk;

using Action = Tvdb.Sdk.Action;
using Type = Tvdb.Sdk.Type;

namespace Jellyfin.Plugin.Tvdb.ScheduledTasks
{
    /// <summary>
    /// Task to poll TheTVDB for updates.
    /// </summary>
    public class UpdateTask : IScheduledTask
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IProviderManager _providerManager;
        private readonly IFileSystem _fileSystem;
        private readonly ILogger<UpdateTask> _logger;
        private readonly TvdbClientManager _tvdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateTask"/> class.
        /// </summary>
        /// <param name="tvdbClientManager">TheTvdb Client Manager.</param>
        /// <param name="libraryManager">Library Manager.</param>
        /// <param name="providerManager">Provider Manager.</param>
        /// <param name="fileSystem">File System.</param>
        /// <param name="logger">Logger.</param>
        public UpdateTask(TvdbClientManager tvdbClientManager, ILibraryManager libraryManager, IProviderManager providerManager, IFileSystem fileSystem, ILogger<UpdateTask> logger)
        {
            _libraryManager = libraryManager;
            _providerManager = providerManager;
            _fileSystem = fileSystem;
            _logger = logger;
            _tvdbClientManager = tvdbClientManager;
        }

        /// <inheritdoc/>
        public string Name => "Check for metadata updates.";

        /// <inheritdoc/>
        public string Key => "CheckForMetadataUpdatesTask";

        /// <inheritdoc/>
        public string Description => "Checks TheTvdb's API Update endpoint for updates periodically and updates metadata accordingly.";

        /// <inheritdoc/>
        public string Category => "TheTVDB";

        private static int MetadataUpdateInHours => TvdbPlugin.Instance?.Configuration.MetadataUpdateInHours * -1 ?? -1;

        /// <inheritdoc/>
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            progress.Report(0);
            _logger.LogInformation("Checking for metadata updates.");
            var toUpdateItems = await GetItemsUpdated(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Found {0} items to update.", toUpdateItems.Count);
            progress.Report(10);
            MetadataRefreshOptions refreshOptions = new MetadataRefreshOptions(new DirectoryService(_fileSystem))
            {
                MetadataRefreshMode = MetadataRefreshMode.FullRefresh
            };
            double increment = 90.0 / toUpdateItems.Count;
            double currentProgress = 10;
            foreach (BaseItem item in toUpdateItems)
            {
                _logger.LogInformation("Refreshing metadata for TvdbId {Tvdbid}:{Name}", item.GetTvdbId(), item.Name);
                await _providerManager.RefreshSingleItem(
                    item,
                    refreshOptions,
                    cancellationToken).ConfigureAwait(false);
                currentProgress += increment;
                progress.Report(currentProgress);
            }

            progress.Report(100);
        }

        /// <inheritdoc/>
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Enumerable.Empty<TaskTriggerInfo>();
        }

        /// <summary>
        /// Gets all items that have been updated.
        /// </summary>
        /// <returns>List of items that have been updated.</returns>
        private async Task<List<BaseItem>> GetItemsUpdated(CancellationToken cancellationToken)
        {
            double fromTime = DateTimeOffset.UtcNow.AddHours(MetadataUpdateInHours).ToUnixTimeSeconds();
            IReadOnlyList<EntityUpdate> episodeUpdates = await _tvdbClientManager.GetUpdates(fromTime, cancellationToken, Type.Episodes, Action.Update).ConfigureAwait(false);
            IReadOnlyList<EntityUpdate> seriesUpdates = await _tvdbClientManager.GetUpdates(fromTime, cancellationToken, Type.Series, Action.Update).ConfigureAwait(false);

            var allUpdates = episodeUpdates.Concat(seriesUpdates);

            string providerId = MetadataProvider.Tvdb.ToString();

            List<BaseItem> toUpdateItems = new List<BaseItem>();

            Dictionary<string, string> providerIdPair = new Dictionary<string, string>() { { providerId, string.Empty } };
            InternalItemsQuery query = new InternalItemsQuery();

            foreach (EntityUpdate update in allUpdates)
            {
                providerIdPair[providerId] = update.RecordId!.Value.ToString(CultureInfo.InvariantCulture);
                query.HasAnyProviderId = providerIdPair;
                List<BaseItem> itemList = _libraryManager.GetItemList(query);
                if (itemList.Count > 0 && !toUpdateItems.Contains(itemList[0]))
                {
                    toUpdateItems.Add(itemList[0]);
                }
            }

            return toUpdateItems;
        }
    }
}
