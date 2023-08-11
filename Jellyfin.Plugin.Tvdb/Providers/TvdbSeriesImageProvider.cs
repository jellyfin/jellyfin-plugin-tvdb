using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using TvDbSharper;
using RatingType = MediaBrowser.Model.Dto.RatingType;
using Series = MediaBrowser.Controller.Entities.TV.Series;

namespace Jellyfin.Plugin.Tvdb.Providers
{
    /// <summary>
    /// Tvdb series image provider.
    /// </summary>
    public class TvdbSeriesImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TvdbSeriesImageProvider> _logger;
        private readonly TvdbClientManager _tvdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TvdbSeriesImageProvider"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger{TvdbSeriesImageProvider}"/> interface.</param>
        /// <param name="tvdbClientManager">Instance of <see cref="TvdbClientManager"/>.</param>
        public TvdbSeriesImageProvider(IHttpClientFactory httpClientFactory, ILogger<TvdbSeriesImageProvider> logger, TvdbClientManager tvdbClientManager)
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
            return item is Series;
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
            if (!TvdbSeriesProvider.IsValidSeries(item.ProviderIds))
            {
                return Enumerable.Empty<RemoteImageInfo>();
            }

            var language = item.GetPreferredMetadataLanguage();
            var remoteImages = new List<RemoteImageInfo>();
            var tvdbId = Convert.ToInt32(item.GetProviderId(TvdbPlugin.ProviderId), CultureInfo.InvariantCulture);
            var seriesInfo = await _tvdbClientManager.GetSeriesImagesAsync(tvdbId, language, cancellationToken).ConfigureAwait(false);
            var seriesImages = seriesInfo.Data.Artworks;
            var languages = _tvdbClientManager.GetLanguagesAsync(CancellationToken.None).Result.Data;
            var artworkTypes = _tvdbClientManager.GetArtworkTypeAsync(CancellationToken.None).Result.Data;
            foreach (var image in seriesImages)
            {
                ImageType type;
                // Checks if valid image type, if not, skip
                try
                {
                    type = TvdbUtils.GetImageTypeFromKeyType(artworkTypes.FirstOrDefault(x => x.Id == image.Type && x.RecordType == "series")?.Name);
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
                // TVDb uses 3 character language
                var imageLanguage = languages.FirstOrDefault(lang => string.Equals(lang.Id, image.Language, StringComparison.OrdinalIgnoreCase))?.Id;
                if (!string.IsNullOrEmpty(imageLanguage))
                {
                    imageInfo.Language = TvdbUtils.NormalizeLanguageToJellyfin(imageLanguage)?.ToLowerInvariant();
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
