using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Tvdb.Providers
{
    /// <inheritdoc />
    public class TvdbEpisodeImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TvdbEpisodeImageProvider> _logger;
        private readonly TvdbClientManager _tvdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TvdbEpisodeImageProvider"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger{TvdbEpisodeImageProvider}"/> interface.</param>
        /// <param name="tvdbClientManager">Instance of <see cref="TvdbClientManager"/>.</param>
        public TvdbEpisodeImageProvider(IHttpClientFactory httpClientFactory, ILogger<TvdbEpisodeImageProvider> logger, TvdbClientManager tvdbClientManager)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _tvdbClientManager = tvdbClientManager;
        }

        /// <inheritdoc />
        public string Name => TvdbPlugin.ProviderName;

        /// <inheritdoc />
        public bool Supports(BaseItem item)
        {
            return item is Episode;
        }

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            yield return ImageType.Primary;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var episode = (Episode)item;
            var series = episode.Series;
            var imageResult = new List<RemoteImageInfo>();
            var language = item.GetPreferredMetadataLanguage();

            var episodeTvdbId = episode.GetTvdbId();

            // Need either a direct episode ID or a supported series with an episode number
            if (episodeTvdbId == 0 && (!series.IsSupported() || !episode.IndexNumber.HasValue))
            {
                return imageResult;
            }

            try
            {
                if (episodeTvdbId == 0)
                {
                    var episodeInfo = new EpisodeInfo
                    {
                        IndexNumber = episode.IndexNumber!.Value,
                        ParentIndexNumber = episode.ParentIndexNumber,
                        SeriesProviderIds = series.ProviderIds,
                        SeriesDisplayOrder = series.DisplayOrder
                    };

                    var episodeTvdbIdStr = await _tvdbClientManager
                        .GetEpisodeTvdbId(episodeInfo, language, cancellationToken).ConfigureAwait(false);
                    episodeTvdbId = Convert.ToInt32(episodeTvdbIdStr, CultureInfo.InvariantCulture);
                }

                if (episodeTvdbId == 0)
                {
                    _logger.LogError(
                        "Episode {SeasonNumber}x{EpisodeNumber} not found for series {SeriesTvdbId}:{Name}",
                        episode.ParentIndexNumber,
                        episode.IndexNumber,
                        series.GetTvdbId(),
                        series.Name);
                    return imageResult;
                }

                var episodeResult = await _tvdbClientManager
                    .GetEpisodesAsync(episodeTvdbId, language, cancellationToken)
                    .ConfigureAwait(false);

                imageResult.AddIfNotNull(episodeResult.CreateImageInfo(Name));
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to retrieve episode images for series {TvDbId}:{Name}", series.GetTvdbId(), series.Name);
            }

            return imageResult;
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);
        }
    }
}
