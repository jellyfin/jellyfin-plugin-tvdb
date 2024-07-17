using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Tvdb.SeasonClient;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Tvdb.Sdk;

namespace Jellyfin.Plugin.Tvdb.Providers
{
    /// <summary>
    /// The Tvdb Season Provider.
    /// </summary>
    public class TvdbSeasonProvider : IRemoteMetadataProvider<Season, SeasonInfo>
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TvdbSeasonProvider> _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly TvdbClientManager _tvdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TvdbSeasonProvider"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger{TvdbSeasonProvider}"/> interface.</param>
        /// <param name="libraryManager">Instance of <see cref="ILibraryManager"/>.</param>
        /// <param name="tvdbClientManager">Instance of <see cref="TvdbClientManager"/>.</param>
        public TvdbSeasonProvider(IHttpClientFactory httpClientFactory, ILogger<TvdbSeasonProvider> logger, ILibraryManager libraryManager, TvdbClientManager tvdbClientManager)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _tvdbClientManager = tvdbClientManager;
            _libraryManager = libraryManager;
        }

        /// <inheritdoc/>
        public string Name => TvdbPlugin.ProviderName;

        private static bool ImportSeasonName => TvdbPlugin.Instance?.Configuration.ImportSeasonName ?? false;

        /// <inheritdoc/>
        public async Task<MetadataResult<Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
        {
            if (info.IndexNumber == null || !info.SeriesProviderIds.IsSupported())
            {
                _logger.LogDebug("No series identity found for {EpisodeName}", info.Name);
                return new MetadataResult<Season>
                {
                    QueriedById = true
                };
            }

            int? seasonId = info.GetTvdbId();
            string displayOrder;

            // If the seasonId is 0, then we have to get the series metadata to get display order and then get the seasonId.
            // If is automated is true, means that display order has changed,do the same as above.
            if (seasonId == 0 || info.IsAutomated)
            {
                info.SeriesProviderIds.TryGetValue(MetadataProvider.Tvdb.ToString(), out var seriesId);
                if (string.IsNullOrWhiteSpace(seriesId))
                {
                    _logger.LogDebug("No series identity found for {EpisodeName}", info.Name);
                    return new MetadataResult<Season>
                    {
                        QueriedById = true
                    };
                }

                var query = new InternalItemsQuery()
                {
                    HasAnyProviderId = new Dictionary<string, string>
                    {
                        { MetadataProvider.Tvdb.ToString(), seriesId }
                    }
                };

                var series = _libraryManager.GetItemList(query).OfType<Series>().FirstOrDefault();
                displayOrder = series!.DisplayOrder;
                if (string.IsNullOrWhiteSpace(displayOrder))
                {
                    displayOrder = "official";
                }

                var seriesInfo = await _tvdbClientManager.GetSeriesExtendedByIdAsync(series.GetTvdbId(), string.Empty, cancellationToken, small: true)
                .ConfigureAwait(false);
                seasonId = seriesInfo.Seasons.FirstOrDefault(s => s.Number == info.IndexNumber && string.Equals(s.Type.Type, displayOrder, StringComparison.OrdinalIgnoreCase))?.Id;

                if (seasonId == null)
                {
                    _logger.LogDebug("No season identity found for {SeasonName}", info.Name);
                    return new MetadataResult<Season>
                    {
                        QueriedById = true
                    };
                }
            }

            var seasonInfo = await _tvdbClientManager.GetSeasonByIdAsync(seasonId ?? 0, string.Empty, cancellationToken)
                .ConfigureAwait(false);

            return MapSeasonToResult(info, seasonInfo);
        }

        private MetadataResult<Season> MapSeasonToResult(SeasonInfo id, CustomSeasonExtendedRecord season)
        {
            var result = new MetadataResult<Season>
            {
                HasMetadata = true,
                Item = new Season
                {
                    IndexNumber = id.IndexNumber,
                    // Tvdb uses 3 letter code for language (prob ISO 639-2)
                    // Reverts to OriginalName if no translation is found
                    Overview = season.Translations.GetTranslatedOverviewOrDefault(id.MetadataLanguage),
                }
            };

            var item = result.Item;
            item.SetTvdbId(season.Id);

            if (ImportSeasonName)
            {
                item.Name = season.Translations.GetTranslatedNamedOrDefaultIgnoreAlias(id.MetadataLanguage) ?? TvdbUtils.ReturnOriginalLanguageOrDefault(season.Name);
                item.OriginalTitle = season.Name;
            }

            return result;
        }

        /// <inheritdoc/>
        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo searchInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
        }

        /// <inheritdoc/>
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);
        }
    }
}
