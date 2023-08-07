using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Caching.Memory;
using TvDbSharper;

namespace Jellyfin.Plugin.Tvdb
{
    /// <summary>
    /// Tvdb client manager.
    /// </summary>
    public class TvdbClientManager
    {
        private const string DefaultLanguage = "en";

        private readonly IMemoryCache _cache;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// TvDbClients per language.
        /// </summary>
        private readonly ConcurrentDictionary<string, TvDbClientInfo> _tvDbClients = new ConcurrentDictionary<string, TvDbClientInfo>();

        /// <summary>
        /// Initializes a new instance of the <see cref="TvdbClientManager"/> class.
        /// </summary>
        /// <param name="memoryCache">Instance of the <see cref="IMemoryCache"/> interface.</param>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        public TvdbClientManager(IMemoryCache memoryCache, IHttpClientFactory httpClientFactory)
        {
            _cache = memoryCache;
            _httpClientFactory = httpClientFactory;
        }

        private static string? ProjectApiKey => TvdbPlugin.Instance?.Configuration.ProjectApiKey;

        private static string? ApiKey => TvdbPlugin.Instance?.Configuration.ApiKey;

        private async Task<TvDbClient> GetTvDbClient(string language)
        {
            var normalizedLanguage = TvdbUtils.NormalizeLanguage(language) ?? DefaultLanguage;

            var tvDbClientInfo = _tvDbClients.GetOrAdd(normalizedLanguage, key => new TvDbClientInfo(_httpClientFactory, key));

            var tvDbClient = tvDbClientInfo.Client;

            // First time authenticating if the token was never updated or if it's empty in the client
            if (tvDbClientInfo.TokenUpdatedAt == DateTime.MinValue || string.IsNullOrEmpty(tvDbClient.AuthToken))
            {
                await tvDbClientInfo.TokenUpdateLock.WaitAsync().ConfigureAwait(false);

                try
                {
                    if (string.IsNullOrEmpty(tvDbClient.AuthToken))
                    {
                        await tvDbClient.Login(ProjectApiKey, ApiKey).ConfigureAwait(false);
                        tvDbClientInfo.TokenUpdatedAt = DateTime.UtcNow;
                    }
                }
                finally
                {
                    tvDbClientInfo.TokenUpdateLock.Release();
                }
            }

            // Refresh if necessary
            if (tvDbClientInfo.TokenUpdatedAt < DateTime.UtcNow.Subtract(TimeSpan.FromDays(25)))
            {
                await tvDbClientInfo.TokenUpdateLock.WaitAsync().ConfigureAwait(false);

                try
                {
                    if (tvDbClientInfo.TokenUpdatedAt < DateTime.UtcNow.Subtract(TimeSpan.FromDays(25)))
                    {
                        await tvDbClient.Login(ProjectApiKey, ApiKey).ConfigureAwait(false);
                        tvDbClientInfo.TokenUpdatedAt = DateTime.UtcNow;
                    }
                }
                finally
                {
                    tvDbClientInfo.TokenUpdateLock.Release();
                }
            }

            return tvDbClient;
        }

        /// <summary>
        /// Get series by name.
        /// </summary>
        /// <param name="name">Series name.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The series search result.</returns>
        public Task<TvDbApiResponse<SearchResultDto[]>> GetSeriesByNameAsync(
            string name,
            string language,
            CancellationToken cancellationToken)
        {
            var cacheKey = GenerateKey("series", name, language);
            SearchOptionalParams optionalParams = new SearchOptionalParams
            {
                Query = name,
                Language = language,
                Type = "series",
            };
            return TryGetValue(cacheKey, language, tvDbClient => tvDbClient.Search(optionalParams, cancellationToken));
        }

        /// <summary>
        /// Get series by id.
        /// </summary>
        /// <param name="tvdbId">The series tvdb id.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The series response.</returns>
        public Task<TvDbApiResponse<SeriesExtendedRecordDto>> GetSeriesByIdAsync(
            int tvdbId,
            string language,
            CancellationToken cancellationToken)
        {
            var cacheKey = GenerateKey("series", tvdbId, language);
            SeriesExtendedOptionalParams optionalParams = new SeriesExtendedOptionalParams
            {
                Meta = "translations",
            };
            return TryGetValue(cacheKey, language, tvDbClient => tvDbClient.SeriesExtended(tvdbId, optionalParams, cancellationToken));
        }

        /// <summary>
        /// Get Season record.
        /// </summary>
        /// <param name="seasonTvdbId">The season tvdb id.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The episode record.</returns>
        public Task<TvDbApiResponse<SeasonExtendedRecordDto>> GetSeasonByIdAsync(
            int seasonTvdbId,
            string language,
            CancellationToken cancellationToken)
        {
            var cacheKey = GenerateKey("season", seasonTvdbId, language);
            return TryGetValue(cacheKey, language, tvDbClient => tvDbClient.SeasonExtended(seasonTvdbId, cancellationToken));
        }

        /// <summary>
        /// Get episode record.
        /// </summary>
        /// <param name="episodeTvdbId">The episode tvdb id.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The episode record.</returns>
        public Task<TvDbApiResponse<EpisodeExtendedRecordDto>> GetEpisodesAsync(
            int episodeTvdbId,
            string language,
            CancellationToken cancellationToken)
        {
            var cacheKey = GenerateKey("episode", episodeTvdbId, language);
            EpisodeExtendedOptionalParams optionalParams = new EpisodeExtendedOptionalParams
            {
                Meta = "translations",
            };
            return TryGetValue(cacheKey, language, tvDbClient => tvDbClient.EpisodeExtended(episodeTvdbId, optionalParams, cancellationToken));
        }

        /// <summary>
        /// Get series by remoteId.
        /// </summary>
        /// <param name="remoteId">The remote id. Supported RemoteIds are: IMDB, TMDB, Zap2It, TV Maze and EIDR.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The series search result.</returns>
        public Task<TvDbApiResponse<SearchByRemoteIdResultDto[]>> GetSeriesByRemoteIdAsync(
            string remoteId,
            string language,
            CancellationToken cancellationToken)
        {
            var cacheKey = GenerateKey("series", remoteId, language);
            return TryGetValue(cacheKey, language, tvDbClient => tvDbClient.SearchResultsByRemoteId(remoteId, cancellationToken));
        }

        /// <summary>
        /// Get actors by tvdb id.
        /// </summary>
        /// <param name="tvdbId">People Tvdb id.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The actors attached to the id.</returns>
        public Task<TvDbApiResponse<PeopleBaseRecordDto>> GetActorAsync(
            int tvdbId,
            string language,
            CancellationToken cancellationToken)
        {
            var cacheKey = GenerateKey("actors", tvdbId, language);
            return TryGetValue(cacheKey, language, tvDbClient => tvDbClient.People(tvdbId, cancellationToken));
        }

        /// <summary>
        /// Get image by image tvdb id.
        /// </summary>
        /// <param name="imageTvdbId"> Tvdb id.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The images attached to the id.</returns>
        public Task<TvDbApiResponse<ArtworkExtendedRecordDto>> GetImageAsync(
            int imageTvdbId,
            string language,
            CancellationToken cancellationToken)
        {
            var cacheKey = GenerateKey("image", imageTvdbId, language);
            return TryGetValue(cacheKey, language, tvDbClient => tvDbClient.ArtworkExtended(imageTvdbId, cancellationToken));
        }

        /// <summary>
        /// Get image by series tvdb id.
        /// </summary>
        /// <param name="tvdbId"> Tvdb id.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The images attached to the id.</returns>
        public Task<TvDbApiResponse<SeriesExtendedRecordDto>> GetSeriesImagesAsync(
            int tvdbId,
            string language,
            CancellationToken cancellationToken)
        {
            var cacheKey = GenerateKey("images", tvdbId, language);
            return TryGetValue(cacheKey, language, tvDbClient => tvDbClient.SeriesArtworks(tvdbId, cancellationToken));
        }

        /// <summary>
        /// Get all tvdb languages.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>All tvdb languages.</returns>
        public Task<TvDbApiResponse<LanguageDto[]>> GetLanguagesAsync(CancellationToken cancellationToken)
        {
            return TryGetValue("languages", string.Empty, tvDbClient => tvDbClient.Languages(cancellationToken));
        }

        /// <summary>
        /// Gets all tvdb artwork types.
        /// </summary>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <returns>All tvdb artwork types.</returns>
        public Task<TvDbApiResponse<ArtworkTypeDto[]>> GetArtworkTypeAsync(CancellationToken cancellationToken)
        {
            return TryGetValue("artworktypes", string.Empty, tvDbClient => tvDbClient.ArtworkTypes(cancellationToken));
        }

        /// <summary>
        /// Get an episode's tvdb id.
        /// </summary>
        /// <param name="searchInfo">Episode search info.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The tvdb id.</returns>
        public async Task<string?> GetEpisodeTvdbId(
            EpisodeInfo searchInfo,
            string language,
            CancellationToken cancellationToken)
        {
            searchInfo.SeriesProviderIds.TryGetValue(TvdbPlugin.ProviderId, out var seriesTvdbId);
            SeriesEpisodesOptionalParams episodeQuery = new SeriesEpisodesOptionalParams();
            // Prefer SxE over premiere date as it is more robust
            if (searchInfo.IndexNumber.HasValue && searchInfo.ParentIndexNumber.HasValue)
            {
                switch (searchInfo.SeriesDisplayOrder)
                {
                    case "dvd":
                        episodeQuery.EpisodeNumber = searchInfo.IndexNumber.Value;
                        episodeQuery.Season = searchInfo.ParentIndexNumber.Value;
                        break;
                    case "absolute":
                        episodeQuery.EpisodeNumber = searchInfo.IndexNumber.Value;
                        break;
                    default:
                        // aired order
                        episodeQuery.EpisodeNumber = searchInfo.IndexNumber.Value;
                        episodeQuery.Season = searchInfo.ParentIndexNumber.Value;
                        break;
                }
            }
            else if (searchInfo.PremiereDate.HasValue)
            {
                // tvdb expects yyyy-mm-dd format
                episodeQuery.AirDate = searchInfo.PremiereDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            TvDbApiResponse<GetSeriesEpisodesResponseData> apiResponse = new TvDbApiResponse<GetSeriesEpisodesResponseData>();
            switch (searchInfo.SeriesDisplayOrder)
            {
                case "dvd":
                case "absolute":
                    apiResponse = await TryGetValue("EpisodeTvdbId", language, tvDbClient => tvDbClient.SeriesEpisodes(Convert.ToInt32(seriesTvdbId, CultureInfo.InvariantCulture), searchInfo.SeriesDisplayOrder, episodeQuery, cancellationToken)).ConfigureAwait(false);
                    break;
                default:
                    apiResponse = await TryGetValue("EpisodeTvdbId", language, tvDbClient => tvDbClient.SeriesEpisodes(Convert.ToInt32(seriesTvdbId, CultureInfo.InvariantCulture), "default", episodeQuery, cancellationToken)).ConfigureAwait(false);
                    break;
            }

            GetSeriesEpisodesResponseData apiData = apiResponse.Data;

            if (apiData == null || apiData.Episodes == null || apiData.Episodes.Length == 0)
            {
                return null;
            }
            else
            {
                return apiData.Episodes[0].Id.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string GenerateKey(params object[] objects)
        {
            var key = string.Empty;

            foreach (var obj in objects)
            {
                var objType = obj.GetType();
                if (objType.IsPrimitive || objType == typeof(string))
                {
                    key += obj + ";";
                }
                else
                {
                    foreach (PropertyInfo propertyInfo in objType.GetProperties())
                    {
                        var currentValue = propertyInfo.GetValue(obj, null);
                        if (currentValue == null)
                        {
                            continue;
                        }

                        key += propertyInfo.Name + "=" + currentValue + ";";
                    }
                }
            }

            return key;
        }

        private Task<T> TryGetValue<T>(string key, string language, Func<TvDbClient, Task<T>> resultFactory)
        {
            return _cache.GetOrCreateAsync(key, async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);

                var tvDbClient = await GetTvDbClient(language).ConfigureAwait(false);

                var result = await resultFactory.Invoke(tvDbClient).ConfigureAwait(false);

                return result;
            });
        }

        private class TvDbClientInfo
        {
            public TvDbClientInfo(IHttpClientFactory httpClientFactory, string language)
            {
                Client = new TvDbClient(httpClientFactory.CreateClient(NamedClient.Default));
                TokenUpdateLock = new SemaphoreSlim(1, 1);
                TokenUpdatedAt = DateTime.MinValue;
            }

            public TvDbClient Client { get; }

            public SemaphoreSlim TokenUpdateLock { get; }

            public DateTime TokenUpdatedAt { get; set; }
        }
    }
}
