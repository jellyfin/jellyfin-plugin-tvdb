using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Tvdb.ScheduledTasks
{
    /// <summary>
    /// Task to purge TheTVDB plugin cache.
    /// </summary>
    public class PurgeCacheTask : IScheduledTask
    {
        private readonly ILogger<PurgeCacheTask> _logger;
        private readonly TvdbClientManager _tvdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="PurgeCacheTask"/> class.
        /// </summary>
        /// <param name="logger">Instance of the <see cref="ILogger{TvdbScheduledTask}"/> interface.</param>
        /// <param name="tvdbClientManager">Instance of <see cref="TvdbClientManager"/>.</param>
        public PurgeCacheTask(
            ILogger<PurgeCacheTask> logger,
            TvdbClientManager tvdbClientManager)
        {
            _logger = logger;
            _tvdbClientManager = tvdbClientManager;
        }

        /// <inheritdoc/>
        public string Name => "Purge TheTVDB plugin cache";

        /// <inheritdoc/>
        public string Key => "PurgeTheTVDBPluginCache";

        /// <inheritdoc/>
        public string Description => "Purges the TheTVDB Cache";

        /// <inheritdoc/>
        public string Category => "TheTVDB";

        /// <inheritdoc/>
        public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            if (_tvdbClientManager.PurgeCache())
            {
                _logger.LogInformation("TheTvdb plugin cache purged successfully");
            }
            else
            {
                _logger.LogError("TheTvdb plugin cache purge failed");
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return Enumerable.Empty<TaskTriggerInfo>();
        }
    }
}
