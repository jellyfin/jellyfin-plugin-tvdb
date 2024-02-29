using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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
    /// Tvdb series provider.
    /// </summary>
    public class TvdbSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>
    {
        private const int MaxSearchResults = 10;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TvdbSeriesProvider> _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly TvdbClientManager _tvdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TvdbSeriesProvider"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger{TvdbSeriesProvider}"/> interface.</param>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        /// <param name="tvdbClientManager">Instance of <see cref="TvdbClientManager"/>.</param>
        public TvdbSeriesProvider(IHttpClientFactory httpClientFactory, ILogger<TvdbSeriesProvider> logger, ILibraryManager libraryManager, TvdbClientManager tvdbClientManager)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _libraryManager = libraryManager;
            _tvdbClientManager = tvdbClientManager;
        }

        /// <inheritdoc />
        public string Name => TvdbPlugin.ProviderName;

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            if (IsValidSeries(searchInfo.ProviderIds))
            {
                return await FetchSeriesSearchResult(searchInfo, cancellationToken).ConfigureAwait(false);
            }

            return await FindSeries(searchInfo.Name, searchInfo.Year, searchInfo.MetadataLanguage, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>
            {
                QueriedById = true,
            };

            if (!IsValidSeries(info.ProviderIds))
            {
                result.QueriedById = false;
                await Identify(info).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (IsValidSeries(info.ProviderIds))
            {
                result.Item = new Series();
                result.HasMetadata = true;

                await FetchSeriesMetadata(result, info, cancellationToken)
                    .ConfigureAwait(false);
            }

            return result;
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);
        }

        /// <summary>
        /// Check whether a dictionary of provider IDs includes an entry for a valid TV metadata provider.
        /// </summary>
        /// <param name="ids">The provider IDs to check.</param>
        /// <returns>True, if the series contains a valid TV provider ID, otherwise false.</returns>
        internal static bool IsValidSeries(Dictionary<string, string> ids)
        {
            return (ids.TryGetValue(MetadataProvider.Tvdb.ToString(), out var tvdbId) && !string.IsNullOrEmpty(tvdbId))
                   || (ids.TryGetValue(MetadataProvider.Imdb.ToString(), out var imdbId) && !string.IsNullOrEmpty(imdbId))
                   || (ids.TryGetValue(MetadataProvider.Zap2It.ToString(), out var zap2ItId) && !string.IsNullOrEmpty(zap2ItId));
        }

        private async Task<IEnumerable<RemoteSearchResult>> FetchSeriesSearchResult(SeriesInfo seriesInfo, CancellationToken cancellationToken)
        {
            var tvdbId = seriesInfo.GetProviderId(MetadataProvider.Tvdb);
            if (string.IsNullOrEmpty(tvdbId))
            {
                var imdbId = seriesInfo.GetProviderId(MetadataProvider.Imdb);
                if (!string.IsNullOrEmpty(imdbId))
                {
                    tvdbId = await GetSeriesByRemoteId(
                        imdbId,
                        seriesInfo.MetadataLanguage,
                        seriesInfo.Name,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            if (string.IsNullOrEmpty(tvdbId))
            {
                var zap2ItId = seriesInfo.GetProviderId(MetadataProvider.Zap2It);
                if (!string.IsNullOrEmpty(zap2ItId))
                {
                    tvdbId = await GetSeriesByRemoteId(
                        zap2ItId,
                        seriesInfo.MetadataLanguage,
                        seriesInfo.Name,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            if (string.IsNullOrEmpty(tvdbId))
            {
                var tmdbId = seriesInfo.GetProviderId(MetadataProvider.Tmdb);
                if (!string.IsNullOrEmpty(tmdbId))
                {
                    tvdbId = await GetSeriesByRemoteId(
                        tmdbId,
                        seriesInfo.MetadataLanguage,
                        seriesInfo.Name,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            try
            {
                var seriesResult =
                    await _tvdbClientManager
                        .GetSeriesExtendedByIdAsync(Convert.ToInt32(tvdbId, CultureInfo.InvariantCulture), seriesInfo.MetadataLanguage, cancellationToken, small: true)
                        .ConfigureAwait(false);
                return new[] { MapSeriesToRemoteSearchResult(seriesResult) };
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to retrieve series with id {TvdbId}:{SeriesName}", tvdbId, seriesInfo.Name);
                return Array.Empty<RemoteSearchResult>();
            }
        }

        private RemoteSearchResult MapSeriesToRemoteSearchResult(SeriesExtendedRecord series)
        {
            var remoteResult = new RemoteSearchResult
            {
                Name = series.Name,
                Overview = series.Overview?.Trim() ?? string.Empty,
                SearchProviderName = Name,
                ImageUrl = series.Image
            };

            if (DateTime.TryParse(series.FirstAired, out var date))
            {
                // Dates from tvdb are either EST or capital of primary airing country.
                remoteResult.PremiereDate = date;
                remoteResult.ProductionYear = date.Year;
            }

            var imdbID = series.RemoteIds.FirstOrDefault(x => x.SourceName == "IMDB")?.Id;
            if (!string.IsNullOrEmpty(imdbID))
            {
                remoteResult.SetProviderId(MetadataProvider.Imdb, imdbID);
            }

            remoteResult.SetProviderId(MetadataProvider.Tvdb, series.Id.GetValueOrDefault().ToString(CultureInfo.InvariantCulture));

            return remoteResult;
        }

        private async Task FetchSeriesMetadata(
            MetadataResult<Series> result,
            SeriesInfo info,
            CancellationToken cancellationToken)
        {
            string metadataLanguage = info.MetadataLanguage;
            Dictionary<string, string> seriesProviderIds = info.ProviderIds;
            var series = result.Item;

            if (seriesProviderIds.TryGetValue(TvdbPlugin.ProviderId, out var tvdbId) && !string.IsNullOrEmpty(tvdbId))
            {
                series.SetProviderId(TvdbPlugin.ProviderId, tvdbId);
            }

            if (seriesProviderIds.TryGetValue(MetadataProvider.Imdb.ToString(), out var imdbId) && !string.IsNullOrEmpty(imdbId))
            {
                series.SetProviderId(MetadataProvider.Imdb, imdbId);
                tvdbId = await GetSeriesByRemoteId(
                    imdbId,
                    metadataLanguage,
                    info.Name,
                    cancellationToken).ConfigureAwait(false);
            }

            if (seriesProviderIds.TryGetValue(MetadataProvider.Zap2It.ToString(), out var zap2It) && !string.IsNullOrEmpty(zap2It))
            {
                series.SetProviderId(MetadataProvider.Zap2It, zap2It);
                tvdbId = await GetSeriesByRemoteId(
                    zap2It,
                    metadataLanguage,
                    info.Name,
                    cancellationToken).ConfigureAwait(false);
            }

            if (seriesProviderIds.TryGetValue(MetadataProvider.Tmdb.ToString(), out var tmdbId) && !string.IsNullOrEmpty(tmdbId))
            {
                series.SetProviderId(MetadataProvider.Tmdb, tmdbId);
                tvdbId = await GetSeriesByRemoteId(
                    tmdbId,
                    metadataLanguage,
                    info.Name,
                    cancellationToken).ConfigureAwait(false);
            }

            try
            {
                var seriesResult =
                    await _tvdbClientManager
                        .GetSeriesExtendedByIdAsync(Convert.ToInt32(tvdbId, CultureInfo.InvariantCulture), metadataLanguage, cancellationToken, Meta4.Translations, false)
                        .ConfigureAwait(false);
                MapSeriesToResult(result, seriesResult, info);

                result.ResetPeople();

                List<Character> people = new List<Character>();
                if (seriesResult.Characters is not null)
                {
                    foreach (Character character in seriesResult.Characters)
                    {
                        people.Add(character);
                    }

                    MapActorsToResult(result, people);
                }
                else
                {
                    _logger.LogError("Failed to retrieve actors for series {TvdbId}:{SeriesName}", tvdbId, info.Name);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to retrieve series with id {TvdbId}:{SeriesName}", tvdbId, info.Name);
                return;
            }
        }

        private async Task<string?> GetSeriesByRemoteId(string remoteId, string language, string seriesName, CancellationToken cancellationToken)
        {
            IReadOnlyList<SearchByRemoteIdResult> resultData;
            try
            {
                resultData = await _tvdbClientManager.GetSeriesByRemoteIdAsync(remoteId, language, cancellationToken)
                            .ConfigureAwait(false);
            }
            catch (SearchException ex) when (ex.InnerException is JsonException)
            {
                _logger.LogError(ex, "Failed to retrieve series with {RemoteId}", remoteId);
                return null;
            }

            if (resultData is null || resultData.Count == 0 || resultData[0]?.Series?.Id is null)
            {
                _logger.LogWarning("TvdbSearch: No series found for remote id: {RemoteId}", remoteId);
                return null;
            }

            return resultData[0].Series.Id?.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Finds the series.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="year">The year.</param>
        /// <param name="language">The language.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Task{System.String}.</returns>
        private async Task<IEnumerable<RemoteSearchResult>> FindSeries(string name, int? year, string language, CancellationToken cancellationToken)
        {
            _logger.LogDebug("TvdbSearch: Finding id for item: {Name} ({Year})", name, year);
            var results = await FindSeriesInternal(name, language, cancellationToken).ConfigureAwait(false);

            return results.Where(i =>
            {
                if (year.HasValue && i.ProductionYear.HasValue)
                {
                    // Allow one year tolerance
                    return Math.Abs(year.Value - i.ProductionYear.Value) <= 1;
                }

                return true;
            });
        }

        private async Task<List<RemoteSearchResult>> FindSeriesInternal(string name, string language, CancellationToken cancellationToken)
        {
            var parsedName = _libraryManager.ParseName(name);
            var comparableName = GetComparableName(parsedName.Name);

            var list = new List<Tuple<List<string>, RemoteSearchResult>>();
            IReadOnlyList<SearchResult> result;
            try
            {
                result = await _tvdbClientManager.GetSeriesByNameAsync(comparableName, language, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "No series results found for {Name}", comparableName);
                return new List<RemoteSearchResult>();
            }

            foreach (var seriesSearchResult in result)
            {
                var tvdbTitles = new List<string>
                {
                    seriesSearchResult.Name
                };
                if (seriesSearchResult.Aliases is not null)
                {
                    tvdbTitles.AddRange(seriesSearchResult.Aliases);
                }

                DateTime? firstAired = null;
                if (DateTime.TryParse(seriesSearchResult.First_air_time, out var parsedFirstAired))
                {
                    firstAired = parsedFirstAired;
                }

                var remoteSearchResult = new RemoteSearchResult
                {
                    Name = tvdbTitles.FirstOrDefault(),
                    ProductionYear = firstAired?.Year,
                    SearchProviderName = Name
                };

                if (!string.IsNullOrEmpty(seriesSearchResult.Image_url))
                {
                    remoteSearchResult.ImageUrl = seriesSearchResult.Image_url;
                }

                try
                {
                    var seriesResult =
                        await _tvdbClientManager.GetSeriesExtendedByIdAsync(Convert.ToInt32(seriesSearchResult.Tvdb_id, CultureInfo.InvariantCulture), language, cancellationToken, small: true)
                            .ConfigureAwait(false);
                    var imdbId = seriesResult.RemoteIds.FirstOrDefault(x => string.Equals(x.SourceName, "IMDB", StringComparison.OrdinalIgnoreCase))?.Id.ToString();
                    if (!string.IsNullOrEmpty(imdbId))
                    {
                        remoteSearchResult.SetProviderId(MetadataProvider.Imdb, imdbId);
                    }

                    var zap2ItId = seriesResult.RemoteIds.FirstOrDefault(x => string.Equals(x.SourceName, "Zap2It", StringComparison.OrdinalIgnoreCase))?.Id.ToString();

                    if (!string.IsNullOrEmpty(zap2ItId))
                    {
                        remoteSearchResult.SetProviderId(MetadataProvider.Zap2It, zap2ItId);
                    }

                    var tmdbId = seriesResult.RemoteIds.FirstOrDefault(x => string.Equals(x.SourceName, "TheMovieDB.com", StringComparison.OrdinalIgnoreCase))?.Id.ToString();

                    if (!string.IsNullOrEmpty(tmdbId))
                    {
                        remoteSearchResult.SetProviderId(MetadataProvider.Tmdb, tmdbId);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unable to retrieve series with id {TvdbId}:{SeriesName}", seriesSearchResult.Tvdb_id, seriesSearchResult.Name);
                }

                remoteSearchResult.SetProviderId(TvdbPlugin.ProviderId, seriesSearchResult.Tvdb_id);
                list.Add(new Tuple<List<string>, RemoteSearchResult>(tvdbTitles, remoteSearchResult));
            }

            return list
                .OrderBy(i => i.Item1.Contains(name, StringComparer.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(i => i.Item1.Any(title => title.Contains(parsedName.Name, StringComparison.OrdinalIgnoreCase)) ? 0 : 1)
                .ThenBy(i => i.Item2.ProductionYear.HasValue && i.Item2.ProductionYear.Equals(parsedName.Year) ? 0 : 1)
                .ThenBy(i => i.Item1.Any(title => title.Contains(comparableName, StringComparison.OrdinalIgnoreCase)) ? 0 : 1)
                .ThenBy(i => list.IndexOf(i))
                .Select(i => i.Item2)
                .Take(MaxSearchResults) // TVDB returns a lot of unrelated results
                .ToList();
        }

        /// <summary>
        /// Gets the name of the comparable.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>System.String.</returns>
        private static string GetComparableName(string name)
        {
            name = name.ToLowerInvariant();
            name = name.Normalize(NormalizationForm.FormC);
            name = name.Replace(", the", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("the ", " ", StringComparison.OrdinalIgnoreCase)
                .Replace(" the ", " ", StringComparison.OrdinalIgnoreCase);
            name = name.Replace("&", " and ", StringComparison.OrdinalIgnoreCase);
            name = Regex.Replace(name, @"[\p{Lm}\p{Mn}]", string.Empty); // Remove diacritics, etc
            name = Regex.Replace(name, @"[\W\p{Pc}]+", " "); // Replace sequences of non-word characters and _ with " "
            return name.Trim();
        }

        private static void MapActorsToResult(MetadataResult<Series> result, IEnumerable<Character> actors)
        {
            foreach (Character actor in actors)
            {
                var personInfo = new PersonInfo
                {
                    Type = PersonType.Actor,
                    Name = (actor.PersonName ?? string.Empty).Trim(),
                    Role = actor.Name
                };

                if (!string.IsNullOrEmpty(actor.PersonImgURL))
                {
                    personInfo.ImageUrl = actor.PersonImgURL;
                }

                if (!string.IsNullOrWhiteSpace(personInfo.Name))
                {
                    result.AddPerson(personInfo);
                }
            }
        }

        private async Task Identify(SeriesInfo info)
        {
            if (!string.IsNullOrWhiteSpace(info.GetProviderId(TvdbPlugin.ProviderId)))
            {
                return;
            }

            var remoteSearchResults = await FindSeries(info.Name, info.Year, info.MetadataLanguage, CancellationToken.None)
                .ConfigureAwait(false);

            var entry = remoteSearchResults.FirstOrDefault();

            if (entry != null)
            {
                var id = entry.GetProviderId(TvdbPlugin.ProviderId);
                if (!string.IsNullOrEmpty(id))
                {
                    info.SetProviderId(TvdbPlugin.ProviderId, id);
                }
            }
        }

        private static void MapSeriesToResult(MetadataResult<Series> result, SeriesExtendedRecord tvdbSeries, SeriesInfo info)
        {
            Series series = result.Item;
            series.SetProviderId(TvdbPlugin.ProviderId, tvdbSeries.Id.GetValueOrDefault().ToString(CultureInfo.InvariantCulture));
            // Tvdb uses 3 letter code for language (prob ISO 639-2)
            // Reverts to OriginalName if no translation is found
            series.Name = tvdbSeries.Translations.GetTranslatedNamedOrDefault(info.MetadataLanguage) ?? tvdbSeries.Name;
            series.Overview = tvdbSeries.Translations.GetTranslatedOverviewOrDefault(info.MetadataLanguage) ?? tvdbSeries.Overview;
            series.OriginalTitle = tvdbSeries.Name;
            result.ResultLanguage = info.MetadataLanguage;
            series.AirDays = TvdbUtils.GetAirDays(tvdbSeries.AirsDays).ToArray();
            series.AirTime = tvdbSeries.AirsTime;
            // series.CommunityRating = (float?)tvdbSeries.SiteRating;
            // Attempts to default to USA if not found
            series.OfficialRating = tvdbSeries.ContentRatings.FirstOrDefault(x => string.Equals(x.Country, TvdbCultureInfo.GetCountryInfo(info.MetadataCountryCode)?.ThreeLetterISORegionName, StringComparison.OrdinalIgnoreCase))?.Name ?? tvdbSeries.ContentRatings.FirstOrDefault(x => string.Equals(x.Country, "usa", StringComparison.OrdinalIgnoreCase))?.Name;
            var imdbId = tvdbSeries.RemoteIds.FirstOrDefault(x => string.Equals(x.SourceName, "IMDB", StringComparison.OrdinalIgnoreCase))?.Id.ToString();
            var zap2ItId = tvdbSeries.RemoteIds.FirstOrDefault(x => string.Equals(x.SourceName, "Zap2It", StringComparison.OrdinalIgnoreCase))?.Id.ToString();
            var tmdbId = tvdbSeries.RemoteIds.FirstOrDefault(x => string.Equals(x.SourceName, "TheMovieDB.com", StringComparison.OrdinalIgnoreCase))?.Id.ToString();
            if (!string.IsNullOrEmpty(imdbId))
            {
                series.SetProviderId(MetadataProvider.Imdb, imdbId);
            }

            if (!string.IsNullOrEmpty(zap2ItId))
            {
                series.SetProviderId(MetadataProvider.Zap2It, zap2ItId);
            }

            if (!string.IsNullOrEmpty(tmdbId))
            {
                series.SetProviderId(MetadataProvider.Tmdb, tmdbId);
            }

            if (Enum.TryParse(tvdbSeries.Status.Name, true, out SeriesStatus seriesStatus))
            {
                series.Status = seriesStatus;
            }

            if (DateTime.TryParse(tvdbSeries.FirstAired, out var date))
            {
                // dates from tvdb are UTC but without offset or Z
                series.PremiereDate = date;
                series.ProductionYear = date.Year;
            }

            if (tvdbSeries.AverageRuntime is not null)
            {
                series.RunTimeTicks = TimeSpan.FromMinutes(tvdbSeries.AverageRuntime.Value).Ticks;
            }

            foreach (var genre in tvdbSeries.Genres)
            {
                series.AddGenre(genre.Name);
            }

            if (tvdbSeries.OriginalNetwork is not null)
            {
                series.AddStudio(tvdbSeries.OriginalNetwork.Name);
            }

            if (result.Item.Status.HasValue && result.Item.Status.Value == SeriesStatus.Ended)
            {
                if (tvdbSeries.Seasons.Count != 0)
                {
                    result.Item.EndDate = DateTime.ParseExact(tvdbSeries.LastAired, "yyyy-mm-dd", CultureInfo.InvariantCulture);
                }
            }
        }
    }
}
