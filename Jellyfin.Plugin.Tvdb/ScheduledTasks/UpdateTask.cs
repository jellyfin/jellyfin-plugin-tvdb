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
        private readonly TvdbClientManager _tvdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateTask"/> class.
        /// </summary>
        /// <param name="tvdbClientManager">TheTvdb Client Manager.</param>
        /// <param name="libraryManager">Library Manager.</param>
        /// <param name="providerManager">Provider Manager.</param>
        /// <param name="fileSystem">File System.</param>
        public UpdateTask(TvdbClientManager tvdbClientManager, ILibraryManager libraryManager, IProviderManager providerManager, IFileSystem fileSystem)
        {
            _libraryManager = libraryManager;
            _providerManager = providerManager;
            _fileSystem = fileSystem;
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
            var toUpdateItems = await GetItemsUpdated(cancellationToken).ConfigureAwait(false);
            foreach (BaseItem item in toUpdateItems)
            {
                await _providerManager.RefreshSingleItem(
                    item,
                    new MetadataRefreshOptions(new DirectoryService(_fileSystem))
                    {
                        MetadataRefreshMode = MetadataRefreshMode.FullRefresh
                    },
                    cancellationToken).ConfigureAwait(false);
            }
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
        /// <exception cref="NotImplementedException">Temp.</exception>
        private async Task<List<BaseItem>> GetItemsUpdated(CancellationToken cancellationToken)
        {
            InternalItemsQuery query = new InternalItemsQuery
            {
                HasTvdbId = true
            };

            List<BaseItem> itemList = _libraryManager.GetItemList(query);

            double fromTime = DateTimeOffset.UtcNow.AddHours(MetadataUpdateInHours).ToUnixTimeSeconds();
            IReadOnlyList<EntityUpdate> episodeUpdates = await _tvdbClientManager.GetUpdates(fromTime, cancellationToken, Type.Episodes, Action.Update).ConfigureAwait(false);
            IReadOnlyList<EntityUpdate> seriesUpdates = await _tvdbClientManager.GetUpdates(fromTime, cancellationToken, Type.Series, Action.Update).ConfigureAwait(false);

            string providerId = MetadataProvider.Tvdb.ToString();
            List<BaseItem> toUpdateItems = itemList.Where(x =>
                episodeUpdates.Any(y => string.Equals(y.RecordId?.ToString(CultureInfo.InvariantCulture), x.ProviderIds[MetadataProvider.Tvdb.ToString()], StringComparison.OrdinalIgnoreCase)) ||
                seriesUpdates.Any(y => string.Equals(y.RecordId?.ToString(CultureInfo.InvariantCulture), x.ProviderIds[MetadataProvider.Tvdb.ToString()], StringComparison.OrdinalIgnoreCase)))
                .ToList();

            return toUpdateItems;
        }
    }
}
