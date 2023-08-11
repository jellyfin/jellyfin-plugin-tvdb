using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
using TvDbSharper;
using RatingType = MediaBrowser.Model.Dto.RatingType;

namespace Jellyfin.Plugin.Tvdb.Providers
{
    /// <summary>
    /// Tvdb season image provider.
    /// </summary>
    public class TvdbSeasonImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TvdbSeasonImageProvider> _logger;
        private readonly TvdbClientManager _tvdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TvdbSeasonImageProvider"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger{TvdbSeasonImageProvider}"/> interface.</param>
        /// <param name="tvdbClientManager">Instance of <see cref="TvdbClientManager"/>.</param>
        public TvdbSeasonImageProvider(IHttpClientFactory httpClientFactory, ILogger<TvdbSeasonImageProvider> logger, TvdbClientManager tvdbClientManager)
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
            return item is Season;
        }

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            yield return ImageType.Primary;
            yield return ImageType.Banner;
            yield return ImageType.Backdrop;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var season = (Season)item;
            var series = season.Series;

            if (series == null || !season.IndexNumber.HasValue || !TvdbSeriesProvider.IsValidSeries(series.ProviderIds))
            {
                return Enumerable.Empty<RemoteImageInfo>();
            }

            var tvdbId = Convert.ToInt32(series.GetProviderId(TvdbPlugin.ProviderId), CultureInfo.InvariantCulture);
            var seasonNumber = season.IndexNumber.Value;
            var language = item.GetPreferredMetadataLanguage();
            var remoteImages = new List<RemoteImageInfo>();
            var seriesInfo = await _tvdbClientManager.GetSeriesExtendedByIdAsync(tvdbId, language, new SeriesExtendedOptionalParams { Short = true }, cancellationToken).ConfigureAwait(false);
            var seasonTvdbId = seriesInfo.Data.Seasons.FirstOrDefault(s => s.Number == seasonNumber)?.Id;

            var seasonInfo = await _tvdbClientManager.GetSeasonByIdAsync(Convert.ToInt32(seasonTvdbId, CultureInfo.InvariantCulture), language, cancellationToken).ConfigureAwait(false);
            var seasonImages = seasonInfo.Data.Artwork;
            var languages = _tvdbClientManager.GetLanguagesAsync(CancellationToken.None).Result.Data;
            var artworkTypes = _tvdbClientManager.GetArtworkTypeAsync(CancellationToken.None).Result.Data;

            foreach (var image in seasonImages)
            {
                ImageType type;
                // Checks if valid image type, if not, skip
                try
                {
                    type = TvdbUtils.GetImageTypeFromKeyType(artworkTypes.FirstOrDefault(x => x.Id == image.Type && x.RecordType == "season")?.Name);
                }
                catch (Exception)
                {
                    continue;
                }

                var imageInfo = new RemoteImageInfo
                {
                    RatingType = RatingType.Score,
                    Url = image.Image,
                    Width = Convert.ToInt32(image.Width, CultureInfo.InvariantCulture),
                    Height = Convert.ToInt32(image.Height, CultureInfo.InvariantCulture),
                    Type = type,
                    ProviderName = Name,
                    ThumbnailUrl = image.Thumbnail
                };

                // Tvdb uses 3 letter code for language (prob ISO 639-2)
                var artworkLanguage = languages.FirstOrDefault(lang => string.Equals(lang.Id, image.Language, StringComparison.OrdinalIgnoreCase))?.Id;
                if (!string.IsNullOrEmpty(artworkLanguage))
                {
                    imageInfo.Language = TvdbUtils.NormalizeLanguageToJellyfin(artworkLanguage)?.ToLowerInvariant();
                }

                remoteImages.Add(imageInfo);
            }

            return remoteImages.OrderByDescending(i =>
            {
                if (string.Equals(language, i.Language, StringComparison.OrdinalIgnoreCase))
                {
                    return 2;
                }
                else if (!string.IsNullOrEmpty(i.Language))
                {
                    return 1;
                }

                return 0;
            });
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);
        }
    }
}
