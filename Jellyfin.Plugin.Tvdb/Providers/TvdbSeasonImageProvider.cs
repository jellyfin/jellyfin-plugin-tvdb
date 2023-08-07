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
            var seriesInfo = await _tvdbClientManager.GetSeriesByIdAsync(tvdbId, language, cancellationToken).ConfigureAwait(false);
            var seasonTvdbId = seriesInfo.Data.Seasons.FirstOrDefault(s => s.Number == seasonNumber)?.Id;

            var seasonInfo = await _tvdbClientManager.GetSeasonByIdAsync(Convert.ToInt32(seasonTvdbId, CultureInfo.InvariantCulture), language, cancellationToken).ConfigureAwait(false);
            var seasonImages = seasonInfo.Data.Artwork;

            foreach (var image in seasonImages)
            {
                try
                {
                    var artworkExtended = await _tvdbClientManager.GetImageAsync(Convert.ToInt32(image.Id, CultureInfo.InvariantCulture), language, cancellationToken).ConfigureAwait(false);
                    remoteImages.AddRange(GetImages(artworkExtended.Data, seasonNumber.ToString(CultureInfo.InvariantCulture), language));
                }
                catch (TvDbServerException)
                {
                    // Ignore
                }
            }

            return remoteImages;
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);
        }

        private IEnumerable<RemoteImageInfo> GetImages(ArtworkExtendedRecordDto image, string seasonNumber, string preferredLanguage)
        {
            var list = new List<RemoteImageInfo>();
            // any languages with null ids are ignored
            var languages = _tvdbClientManager.GetLanguagesAsync(CancellationToken.None).Result.Data.Where(x => !string.IsNullOrEmpty(x.Id)).ToArray();
            var imageInfo = new RemoteImageInfo
            {
                RatingType = RatingType.Score,
                Url = image.Image,
                ProviderName = Name,
                Language = languages.FirstOrDefault(lang => lang.Id == image.Language)?.Name,
                ThumbnailUrl = TvdbUtils.BannerUrl + image.Thumbnail
            };

            imageInfo.Width = Convert.ToInt32(image.Width, CultureInfo.InvariantCulture);
            imageInfo.Height = Convert.ToInt32(image.Height, CultureInfo.InvariantCulture);

            // imageInfo.Type = TvdbUtils.GetImageTypeFromKeyType(image.Type);
            list.Add(imageInfo);

            var isLanguageEn = string.Equals(preferredLanguage, "en", StringComparison.OrdinalIgnoreCase);
            return list.OrderByDescending(i =>
                {
                    if (string.Equals(preferredLanguage, i.Language, StringComparison.OrdinalIgnoreCase))
                    {
                        return 3;
                    }

                    if (!isLanguageEn)
                    {
                        if (string.Equals("en", i.Language, StringComparison.OrdinalIgnoreCase))
                        {
                            return 2;
                        }
                    }

                    if (string.IsNullOrEmpty(i.Language))
                    {
                        return isLanguageEn ? 3 : 2;
                    }

                    return 0;
                })
                .ThenByDescending(i => i.CommunityRating ?? 0)
                .ThenByDescending(i => i.VoteCount ?? 0);
        }
    }
}
