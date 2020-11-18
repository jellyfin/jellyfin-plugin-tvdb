using System;
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
using TvDbSharper.Dto;

namespace Jellyfin.Plugin.Tvdb
{
    /// <summary>
    /// Tvdb client manager.
    /// </summary>
    public class TvdbClientManager
    {
        private const string DefaultLanguage = "en";

        private readonly IMemoryCache _cache;
        private readonly TvDbClient _tvDbClient;
        private DateTime _tokenCreatedAt;

        /// <summary>
        /// Initializes a new instance of the <see cref="TvdbClientManager"/> class.
        /// </summary>
        /// <param name="memoryCache">Instance of the <see cref="IMemoryCache"/> interface.</param>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        public TvdbClientManager(IMemoryCache memoryCache, IHttpClientFactory httpClientFactory)
        {
            _cache = memoryCache;
            _tvDbClient = new TvDbClient(httpClientFactory.CreateClient(NamedClient.Default));
        }

        private static string? ApiKey => TvdbPlugin.Instance?.Configuration.ApiKey;

        private TvDbClient TvDbClient
        {
            get
            {
                if (string.IsNullOrEmpty(_tvDbClient.Authentication.Token))
                {
                    _tvDbClient.Authentication.AuthenticateAsync(ApiKey).GetAwaiter().GetResult();
                    _tokenCreatedAt = DateTime.Now;
                }

                // Refresh if necessary
                if (_tokenCreatedAt < DateTime.Now.Subtract(TimeSpan.FromHours(20)))
                {
                    try
                    {
                        _tvDbClient.Authentication.RefreshTokenAsync().GetAwaiter().GetResult();
                    }
                    catch
                    {
                        _tvDbClient.Authentication.AuthenticateAsync(ApiKey).GetAwaiter().GetResult();
                    }

                    _tokenCreatedAt = DateTime.Now;
                }

                return _tvDbClient;
            }
        }

        /// <summary>
        /// Get series by name.
        /// </summary>
        /// <param name="name">Series name.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The series search result.</returns>
        public Task<TvDbResponse<SeriesSearchResult[]>> GetSeriesByNameAsync(
            string name,
            string language,
            CancellationToken cancellationToken)
        {
            var cacheKey = GenerateKey("series", name, language);
            return TryGetValue(cacheKey, language, () => TvDbClient.Search.SearchSeriesByNameAsync(name, cancellationToken));
        }

        /// <summary>
        /// Get series by id.
        /// </summary>
        /// <param name="tvdbId">The series tvdb id.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The series response.</returns>
        public Task<TvDbResponse<Series>> GetSeriesByIdAsync(
            int tvdbId,
            string language,
            CancellationToken cancellationToken)
        {
            var cacheKey = GenerateKey("series", tvdbId, language);
            return TryGetValue(cacheKey, language, () => TvDbClient.Series.GetAsync(tvdbId, cancellationToken));
        }

        /// <summary>
        /// Get episode record.
        /// </summary>
        /// <param name="episodeTvdbId">The episode tvdb id.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The episode record.</returns>
        public Task<TvDbResponse<EpisodeRecord>> GetEpisodesAsync(
            int episodeTvdbId,
            string language,
            CancellationToken cancellationToken)
        {
            var cacheKey = GenerateKey("episode", episodeTvdbId, language);
            return TryGetValue(cacheKey, language, () => TvDbClient.Episodes.GetAsync(episodeTvdbId, cancellationToken));
        }

        /// <summary>
        /// Get series by imdb.
        /// </summary>
        /// <param name="imdbId">The imdb id.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The series search result.</returns>
        public Task<TvDbResponse<SeriesSearchResult[]>> GetSeriesByImdbIdAsync(
            string imdbId,
            string language,
            CancellationToken cancellationToken)
        {
            var cacheKey = GenerateKey("series", imdbId, language);
            return TryGetValue(cacheKey, language, () => TvDbClient.Search.SearchSeriesByImdbIdAsync(imdbId, cancellationToken));
        }

        /// <summary>
        /// Get series by zap2it id.
        /// </summary>
        /// <param name="zap2ItId">Zap2it id.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The series search result.</returns>
        public Task<TvDbResponse<SeriesSearchResult[]>> GetSeriesByZap2ItIdAsync(
            string zap2ItId,
            string language,
            CancellationToken cancellationToken)
        {
            var cacheKey = GenerateKey("series", zap2ItId, language);
            return TryGetValue(cacheKey, language, () => TvDbClient.Search.SearchSeriesByZap2ItIdAsync(zap2ItId, cancellationToken));
        }

        /// <summary>
        /// Get actors by tvdb id.
        /// </summary>
        /// <param name="tvdbId">Tvdb id.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The actors attached to the id.</returns>
        public Task<TvDbResponse<Actor[]>> GetActorsAsync(
            int tvdbId,
            string language,
            CancellationToken cancellationToken)
        {
            var cacheKey = GenerateKey("actors", tvdbId, language);
            return TryGetValue(cacheKey, language, () => TvDbClient.Series.GetActorsAsync(tvdbId, cancellationToken));
        }

        /// <summary>
        /// Get images by tvdb id.
        /// </summary>
        /// <param name="tvdbId">Tvdb id.</param>
        /// <param name="imageQuery">The image query.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The images attached to the id.</returns>
        public Task<TvDbResponse<Image[]>> GetImagesAsync(
            int tvdbId,
            ImagesQuery imageQuery,
            string language,
            CancellationToken cancellationToken)
        {
            var cacheKey = GenerateKey("images", tvdbId, language, imageQuery);
            return TryGetValue(cacheKey, language, () => TvDbClient.Series.GetImagesAsync(tvdbId, imageQuery, cancellationToken));
        }

        /// <summary>
        /// Get all tvdb languages.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>All tvdb languages.</returns>
        public Task<TvDbResponse<Language[]>> GetLanguagesAsync(CancellationToken cancellationToken)
        {
            return TryGetValue("languages", null, () => TvDbClient.Languages.GetAllAsync(cancellationToken));
        }

        /// <summary>
        /// Get series episode summary.
        /// </summary>
        /// <param name="tvdbId">Tvdb id.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The episode summary.</returns>
        public Task<TvDbResponse<EpisodesSummary>> GetSeriesEpisodeSummaryAsync(
            int tvdbId,
            string language,
            CancellationToken cancellationToken)
        {
            var cacheKey = GenerateKey("seriesepisodesummary", tvdbId, language);
            return TryGetValue(cacheKey, language, () => TvDbClient.Series.GetEpisodesSummaryAsync(tvdbId, cancellationToken));
        }

        /// <summary>
        /// Gets a page of episodes.
        /// </summary>
        /// <param name="tvdbId">Tvdb series id.</param>
        /// <param name="page">Episode page.</param>
        /// <param name="episodeQuery">Episode query.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The page of episodes.</returns>
        public Task<TvDbResponse<EpisodeRecord[]>> GetEpisodesPageAsync(
            int tvdbId,
            int page,
            EpisodeQuery episodeQuery,
            string language,
            CancellationToken cancellationToken)
        {
            var cacheKey = GenerateKey(language, tvdbId, episodeQuery);

            return TryGetValue(cacheKey, language, () => TvDbClient.Series.GetEpisodesAsync(tvdbId, page, episodeQuery, cancellationToken));
        }

        /// <summary>
        /// Get an episode's tvdb id.
        /// </summary>
        /// <param name="searchInfo">Episode search info.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The tvdb id.</returns>
        public Task<string?> GetEpisodeTvdbId(
            EpisodeInfo searchInfo,
            string language,
            CancellationToken cancellationToken)
        {
            searchInfo.SeriesProviderIds.TryGetValue(TvdbPlugin.ProviderId, out var seriesTvdbId);

            var episodeQuery = new EpisodeQuery();

            // Prefer SxE over premiere date as it is more robust
            if (searchInfo.IndexNumber.HasValue && searchInfo.ParentIndexNumber.HasValue)
            {
                switch (searchInfo.SeriesDisplayOrder)
                {
                    case "dvd":
                        episodeQuery.DvdEpisode = searchInfo.IndexNumber.Value;
                        episodeQuery.DvdSeason = searchInfo.ParentIndexNumber.Value;
                        break;
                    case "absolute":
                        episodeQuery.AbsoluteNumber = searchInfo.IndexNumber.Value;
                        break;
                    default:
                        // aired order
                        episodeQuery.AiredEpisode = searchInfo.IndexNumber.Value;
                        episodeQuery.AiredSeason = searchInfo.ParentIndexNumber.Value;
                        break;
                }
            }
            else if (searchInfo.PremiereDate.HasValue)
            {
                // tvdb expects yyyy-mm-dd format
                episodeQuery.FirstAired = searchInfo.PremiereDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            return GetEpisodeTvdbId(Convert.ToInt32(seriesTvdbId, CultureInfo.InvariantCulture), episodeQuery, language, cancellationToken);
        }

        /// <summary>
        /// Get an episode's tvdb id.
        /// </summary>
        /// <param name="seriesTvdbId">The series tvdb id.</param>
        /// <param name="episodeQuery">Episode query.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The tvdb id.</returns>
        public async Task<string?> GetEpisodeTvdbId(
            int seriesTvdbId,
            EpisodeQuery episodeQuery,
            string language,
            CancellationToken cancellationToken)
        {
            var episodePage =
                await GetEpisodesPageAsync(Convert.ToInt32(seriesTvdbId), episodeQuery, language, cancellationToken)
                    .ConfigureAwait(false);
            return episodePage.Data.FirstOrDefault()?.Id.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Get episode page.
        /// </summary>
        /// <param name="tvdbId">Tvdb series id.</param>
        /// <param name="episodeQuery">Episode query.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The page of episodes.</returns>
        public Task<TvDbResponse<EpisodeRecord[]>> GetEpisodesPageAsync(
            int tvdbId,
            EpisodeQuery episodeQuery,
            string language,
            CancellationToken cancellationToken)
        {
            return GetEpisodesPageAsync(tvdbId, 1, episodeQuery, language, cancellationToken);
        }

        /// <summary>
        /// Get image key types for series.
        /// </summary>
        /// <param name="tvdbId">Tvdb series id.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The image key types.</returns>
        public async IAsyncEnumerable<KeyType> GetImageKeyTypesForSeriesAsync(int tvdbId, string language, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var cacheKey = GenerateKey(nameof(TvDbClient.Series.GetImagesSummaryAsync), tvdbId);
            var imagesSummary = await TryGetValue(cacheKey, language, () => TvDbClient.Series.GetImagesSummaryAsync(tvdbId, cancellationToken)).ConfigureAwait(false);

            if (imagesSummary.Data.Fanart > 0)
            {
                yield return KeyType.Fanart;
            }

            if (imagesSummary.Data.Series > 0)
            {
                yield return KeyType.Series;
            }

            if (imagesSummary.Data.Poster > 0)
            {
                yield return KeyType.Poster;
            }
        }

        /// <summary>
        /// Get image key types for season.
        /// </summary>
        /// <param name="tvdbId">Tvdb series id.</param>
        /// <param name="language">Metadata language.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The image key types.</returns>
        public async IAsyncEnumerable<KeyType> GetImageKeyTypesForSeasonAsync(int tvdbId, string language, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var cacheKey = GenerateKey(nameof(TvDbClient.Series.GetImagesSummaryAsync), tvdbId);
            var imagesSummary = await TryGetValue(cacheKey, language, () => TvDbClient.Series.GetImagesSummaryAsync(tvdbId, cancellationToken)).ConfigureAwait(false);

            if (imagesSummary.Data.Season > 0)
            {
                yield return KeyType.Season;
            }

            if (imagesSummary.Data.Fanart > 0)
            {
                yield return KeyType.Fanart;
            }

            // TODO seasonwide is not supported in TvDbSharper
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

        private async Task<T> TryGetValue<T>(string key, string? language, Func<Task<T>> resultFactory)
        {
            if (_cache.TryGetValue(key, out T cachedValue))
            {
                return cachedValue;
            }

            _tvDbClient.AcceptedLanguage = TvdbUtils.NormalizeLanguage(language) ?? DefaultLanguage;
            var result = await resultFactory.Invoke().ConfigureAwait(false);
            _cache.Set(key, result, TimeSpan.FromHours(1));
            return result;
        }
    }
}
