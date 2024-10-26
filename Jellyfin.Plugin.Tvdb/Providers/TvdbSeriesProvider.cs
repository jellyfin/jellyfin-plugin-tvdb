using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Extensions;
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
            if (searchInfo.IsSupported())
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

            if (!info.IsSupported())
            {
                result.QueriedById = false;
                await Identify(info).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (info.IsSupported())
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

        private async Task<IEnumerable<RemoteSearchResult>> FetchSeriesSearchResult(SeriesInfo seriesInfo, CancellationToken cancellationToken)
        {
            async Task<string?> TryGetTvdbIdWithRemoteId(MetadataProvider metadataProvider)
            {
                var id = seriesInfo.GetProviderId(metadataProvider);
                if (string.IsNullOrEmpty(id))
                {
                    return null;
                }

                return await GetSeriesByRemoteId(
                    id,
                    seriesInfo.MetadataLanguage,
                    seriesInfo.Name,
                    cancellationToken).ConfigureAwait(false);
            }

            int? tvdbId;
            if (seriesInfo.HasTvdbId())
            {
                tvdbId = seriesInfo.GetTvdbId();
            }
            else
            {
                var tvdbIdTxt = await TryGetTvdbIdWithRemoteId(MetadataProvider.Imdb).ConfigureAwait(false)
                    ?? await TryGetTvdbIdWithRemoteId(MetadataProvider.Zap2It).ConfigureAwait(false)
                    ?? await TryGetTvdbIdWithRemoteId(MetadataProvider.Tmdb).ConfigureAwait(false);

                tvdbId = tvdbIdTxt is null ? null : Convert.ToInt32(tvdbIdTxt, CultureInfo.InvariantCulture);
            }

            if (!tvdbId.HasValue)
            {
                _logger.LogWarning("No valid tvdb id found for series {TvdbId}:{SeriesName}", tvdbId, seriesInfo.Name);
                return Array.Empty<RemoteSearchResult>();
            }

            try
            {
                var seriesResult =
                    await _tvdbClientManager
                        .GetSeriesExtendedByIdAsync(tvdbId.Value, seriesInfo.MetadataLanguage, cancellationToken, meta: Meta4.Translations, small: true)
                        .ConfigureAwait(false);
                return new[] { MapSeriesToRemoteSearchResult(seriesResult, seriesInfo.MetadataLanguage) };
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to retrieve series with id {TvdbId}:{SeriesName}", tvdbId, seriesInfo.Name);
                return Array.Empty<RemoteSearchResult>();
            }
        }

        private RemoteSearchResult MapSeriesToRemoteSearchResult(SeriesExtendedRecord series, string language)
        {
            var remoteResult = new RemoteSearchResult
            {
                Name = series.Translations.GetTranslatedNamedOrDefault(language) ?? TvdbUtils.ReturnOriginalLanguageOrDefault(series.Name),
                Overview = series.Translations.GetTranslatedOverviewOrDefault(language)?.Trim() ?? TvdbUtils.ReturnOriginalLanguageOrDefault(series.Overview?.Trim()),
                SearchProviderName = Name,
                ImageUrl = series.Image
            };

            if (DateTime.TryParse(series.FirstAired, out var date))
            {
                // Dates from tvdb are either EST or capital of primary airing country.
                remoteResult.PremiereDate = date;
                remoteResult.ProductionYear = date.Year;
            }

            var imdbID = series.RemoteIds?.FirstOrDefault(x => x.SourceName == "IMDB")?.Id;
            remoteResult.SetProviderIdIfHasValue(MetadataProvider.Imdb, imdbID);
            remoteResult.SetTvdbId(series.Id);

            return remoteResult;
        }

        private async Task FetchSeriesMetadata(
            MetadataResult<Series> result,
            SeriesInfo seriesInfo,
            CancellationToken cancellationToken)
        {
            var seriesMetadata = result.Item;
            async Task<string?> TryGetTvdbIdWithRemoteId(string id)
            {
                return await GetSeriesByRemoteId(
                    id,
                    seriesInfo.MetadataLanguage,
                    seriesInfo.Name,
                    cancellationToken).ConfigureAwait(false);
            }

            if (seriesInfo.HasTvdbId(out var tvdbIdTxt))
            {
                seriesMetadata.SetTvdbId(tvdbIdTxt);
            }

            if (seriesInfo.HasProviderId(MetadataProvider.Imdb, out var imdbId))
            {
                seriesMetadata.SetProviderId(MetadataProvider.Imdb, imdbId!);
                tvdbIdTxt ??= await TryGetTvdbIdWithRemoteId(imdbId!).ConfigureAwait(false);
            }

            if (seriesInfo.HasProviderId(MetadataProvider.Zap2It, out var zap2It))
            {
                seriesMetadata.SetProviderId(MetadataProvider.Zap2It, zap2It!);
                tvdbIdTxt ??= await TryGetTvdbIdWithRemoteId(zap2It!).ConfigureAwait(false);
            }

            if (seriesInfo.HasProviderId(MetadataProvider.Tmdb, out var tmdbId))
            {
                seriesMetadata.SetProviderId(MetadataProvider.Tmdb, tmdbId!);
                tvdbIdTxt ??= await TryGetTvdbIdWithRemoteId(tmdbId!).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(tvdbIdTxt))
            {
                _logger.LogWarning("No valid tvdb id found for series {TvdbId}:{SeriesName}", tvdbIdTxt, seriesInfo.Name);
                return;
            }

            var tvdbId = Convert.ToInt32(tvdbIdTxt, CultureInfo.InvariantCulture);
            try
            {
                var seriesResult =
                    await _tvdbClientManager
                        .GetSeriesExtendedByIdAsync(tvdbId, seriesInfo.MetadataLanguage, cancellationToken, Meta4.Translations, false)
                        .ConfigureAwait(false);
                MapSeriesToResult(result, seriesResult, seriesInfo);

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
                    _logger.LogError("Failed to retrieve actors for series {TvdbId}:{SeriesName}", tvdbId, seriesInfo.Name);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to retrieve series with id {TvdbId}:{SeriesName}", tvdbId, seriesInfo.Name);
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
            var comparableName = TvdbUtils.GetComparableName(parsedName.Name);

            var list = new List<(List<string> Titles, RemoteSearchResult SearchResult)>();
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
                    seriesSearchResult.Translations.GetTranslatedNamedOrDefault(language) ?? seriesSearchResult.Name
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

                    var imdbId = seriesResult.RemoteIds?.FirstOrDefault(x => string.Equals(x.SourceName, "IMDB", StringComparison.OrdinalIgnoreCase))?.Id.ToString();
                    remoteSearchResult.SetProviderIdIfHasValue(MetadataProvider.Imdb, imdbId);

                    var zap2ItId = seriesResult.RemoteIds?.FirstOrDefault(x => string.Equals(x.SourceName, "Zap2It", StringComparison.OrdinalIgnoreCase))?.Id.ToString();
                    remoteSearchResult.SetProviderIdIfHasValue(MetadataProvider.Zap2It, zap2ItId);

                    var tmdbId = seriesResult.RemoteIds?.FirstOrDefault(x => string.Equals(x.SourceName, "TheMovieDB.com", StringComparison.OrdinalIgnoreCase))?.Id.ToString();

                    // Sometimes, tvdb will return tmdbid as {tmdbid}-{title} like in the tmdb url. Grab the tmdbid only.
                    var tmdbIdLeft = StringExtensions.LeftPart(tmdbId, '-').ToString();
                    remoteSearchResult.SetProviderIdIfHasValue(MetadataProvider.Tmdb, tmdbIdLeft);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unable to retrieve series with id {TvdbId}:{SeriesName}", seriesSearchResult.Tvdb_id, seriesSearchResult.Name);
                }

                remoteSearchResult.SetTvdbId(seriesSearchResult.Tvdb_id);
                list.Add((tvdbTitles, remoteSearchResult));
            }

            return list
                .OrderBy(i => i.Titles.Contains(name, StringComparer.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(i => i.Titles.Any(title => title.Contains(parsedName.Name, StringComparison.OrdinalIgnoreCase)) ? 0 : 1)
                .ThenBy(i => i.SearchResult.ProductionYear.HasValue && i.Item2.ProductionYear.Equals(parsedName.Year) ? 0 : 1)
                .ThenBy(i => i.Titles.Any(title => title.Contains(comparableName, StringComparison.OrdinalIgnoreCase)) ? 0 : 1)
                .ThenBy(i => list.IndexOf(i))
                .Select(i => i.SearchResult)
                .ToList();
        }

        private static void MapActorsToResult(MetadataResult<Series> result, IEnumerable<Character> actors)
        {
            foreach (Character actor in actors)
            {
                var personInfo = new PersonInfo
                {
                    Type = PersonKind.Actor,
                    Name = (actor.PersonName ?? string.Empty).Trim(),
                    Role = actor.Name,
                };

                if (!string.IsNullOrEmpty(actor.PersonImgURL))
                {
                    personInfo.ImageUrl = actor.PersonImgURL;
                }

                if (actor.PeopleId.HasValue)
                {
                    personInfo.SetTvdbId(actor.PeopleId.Value);
                }

                if (!string.IsNullOrWhiteSpace(personInfo.Name))
                {
                    result.AddPerson(personInfo);
                }
            }
        }

        private async Task Identify(SeriesInfo info)
        {
            if (info.HasTvdbId())
            {
                return;
            }

            var remoteSearchResults = await FindSeries(info.Name, info.Year, info.MetadataLanguage, CancellationToken.None)
                .ConfigureAwait(false);

            var entry = remoteSearchResults.FirstOrDefault();
            if (entry.HasTvdbId(out var tvdbId))
            {
                info.SetTvdbId(tvdbId);
            }
        }

        private void MapSeriesToResult(MetadataResult<Series> result, SeriesExtendedRecord tvdbSeries, SeriesInfo info)
        {
            Series series = result.Item;
            series.SetTvdbId(tvdbSeries.Id);
            // Tvdb uses 3 letter code for language (prob ISO 639-2)
            // Reverts to OriginalName if no translation is found
            series.Name = tvdbSeries.Translations.GetTranslatedNamedOrDefault(info.MetadataLanguage) ?? TvdbUtils.ReturnOriginalLanguageOrDefault(tvdbSeries.Name);
            series.Overview = tvdbSeries.Translations.GetTranslatedOverviewOrDefault(info.MetadataLanguage) ?? TvdbUtils.ReturnOriginalLanguageOrDefault(tvdbSeries.Overview);
            series.OriginalTitle = tvdbSeries.Name;
            result.ResultLanguage = info.MetadataLanguage;
            series.AirDays = TvdbUtils.GetAirDays(tvdbSeries.AirsDays).ToArray();
            series.AirTime = tvdbSeries.AirsTime;
            // series.CommunityRating = (float?)tvdbSeries.SiteRating;
            // Attempts to default to USA if not found
            series.OfficialRating = tvdbSeries.ContentRatings?.FirstOrDefault(x => string.Equals(x.Country, TvdbCultureInfo.GetCountryInfo(info.MetadataCountryCode)?.ThreeLetterISORegionName, StringComparison.OrdinalIgnoreCase))?.Name ?? tvdbSeries.ContentRatings?.FirstOrDefault(x => string.Equals(x.Country, "usa", StringComparison.OrdinalIgnoreCase))?.Name;
            if (tvdbSeries.Lists is not null && tvdbSeries.Lists is JsonElement jsonElement)
            {
                var collections = jsonElement.Deserialize<List<object>>();
                if (collections is not null)
                {
                    try
                    {
                        var collectionIds = collections.OfType<JsonElement>()
                            .Where(x => x.GetProperty("isOfficial").GetBoolean())
                            .Select(x => x.GetProperty("id").GetInt32().ToString(CultureInfo.InvariantCulture))
                            .Aggregate(new StringBuilder(), (sb, id) => sb.Append(id).Append(';'));

                        series.SetProviderIdIfHasValue(TvdbPlugin.CollectionProviderId, collectionIds.ToString());
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Failed to retrieve collection for series {TvdbId}:{SeriesName}", tvdbSeries.Id, tvdbSeries.Name);
                    }
                }
            }

            var imdbId = tvdbSeries.RemoteIds?.FirstOrDefault(x => string.Equals(x.SourceName, "IMDB", StringComparison.OrdinalIgnoreCase))?.Id.ToString();
            series.SetProviderIdIfHasValue(MetadataProvider.Imdb, imdbId);

            var zap2ItId = tvdbSeries.RemoteIds?.FirstOrDefault(x => string.Equals(x.SourceName, "Zap2It", StringComparison.OrdinalIgnoreCase))?.Id.ToString();
            series.SetProviderIdIfHasValue(MetadataProvider.Zap2It, zap2ItId);

            var tmdbId = tvdbSeries.RemoteIds?.FirstOrDefault(x => string.Equals(x.SourceName, "TheMovieDB.com", StringComparison.OrdinalIgnoreCase))?.Id.ToString();
            series.SetProviderIdIfHasValue(MetadataProvider.Tmdb, tmdbId);

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
