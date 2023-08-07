using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using TvDbSharper;
using Series = MediaBrowser.Controller.Entities.TV.Series;

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
                        MetadataProvider.Imdb.ToString(),
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
                        MetadataProvider.Zap2It.ToString(),
                        seriesInfo.MetadataLanguage,
                        seriesInfo.Name,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            try
            {
                var seriesResult =
                    await _tvdbClientManager
                        .GetSeriesByIdAsync(Convert.ToInt32(tvdbId, CultureInfo.InvariantCulture), seriesInfo.MetadataLanguage, cancellationToken)
                        .ConfigureAwait(false);
                return new[] { MapSeriesToRemoteSearchResult(seriesResult.Data) };
            }
            catch (TvDbServerException e)
            {
                _logger.LogError(e, "Failed to retrieve series with id {TvdbId}:{SeriesName}", tvdbId, seriesInfo.Name);
                return Array.Empty<RemoteSearchResult>();
            }
        }

        private RemoteSearchResult MapSeriesToRemoteSearchResult(SeriesExtendedRecordDto series)
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

            remoteResult.SetProviderId(MetadataProvider.Tvdb, series.Id.ToString(CultureInfo.InvariantCulture));

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
                    MetadataProvider.Imdb.ToString(),
                    metadataLanguage,
                    info.Name,
                    cancellationToken).ConfigureAwait(false);
            }

            if (seriesProviderIds.TryGetValue(MetadataProvider.Zap2It.ToString(), out var zap2It) && !string.IsNullOrEmpty(zap2It))
            {
                series.SetProviderId(MetadataProvider.Zap2It, zap2It);
                tvdbId = await GetSeriesByRemoteId(
                    zap2It,
                    MetadataProvider.Zap2It.ToString(),
                    metadataLanguage,
                    info.Name,
                    cancellationToken).ConfigureAwait(false);
            }

            try
            {
                var seriesResult =
                    await _tvdbClientManager
                        .GetSeriesByIdAsync(Convert.ToInt32(tvdbId, CultureInfo.InvariantCulture), metadataLanguage, cancellationToken)
                        .ConfigureAwait(false);
                await MapSeriesToResult(result, seriesResult.Data, metadataLanguage).ConfigureAwait(false);
            }
            catch (TvDbServerException e)
            {
                _logger.LogError(e, "Failed to retrieve series with id {TvdbId}:{SeriesName}", tvdbId, info.Name);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            result.ResetPeople();

            try
            {
                var actorsResult = await _tvdbClientManager
                    .GetActorsAsync(Convert.ToInt32(tvdbId, CultureInfo.InvariantCulture), metadataLanguage, cancellationToken).ConfigureAwait(false);
                MapActorsToResult(result, actorsResult.Data);
            }
            catch (TvDbServerException e)
            {
                _logger.LogError(e, "Failed to retrieve actors for series {TvdbId}:{SeriesName}", tvdbId, info.Name);
            }
        }

        private async Task<string?> GetSeriesByRemoteId(string id, string idType, string language, string seriesName, CancellationToken cancellationToken)
        {
            TvDbResponse<SeriesSearchResult[]>? result = null;

            try
            {
                if (string.Equals(idType, MetadataProvider.Zap2It.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    result = await _tvdbClientManager.GetSeriesByZap2ItIdAsync(id, language, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    result = await _tvdbClientManager.GetSeriesByImdbIdAsync(id, language, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (TvDbServerException e)
            {
                _logger.LogError(e, "Failed to retrieve series with remote id {RemoteId}:{SeriesName}", id, seriesName);
            }

            return result?.Data.FirstOrDefault()?.Id.ToString(CultureInfo.InvariantCulture);
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
            _logger.LogDebug("TvdbSearch: Finding id for item: {0} ({1})", name, year);
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
            TvDbResponse<SeriesSearchResult[]> result;
            try
            {
                result = await _tvdbClientManager.GetSeriesByNameAsync(comparableName, language, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TvDbServerException e)
            {
                _logger.LogError(e, "No series results found for {Name}", comparableName);
                return new List<RemoteSearchResult>();
            }

            foreach (var seriesSearchResult in result.Data)
            {
                var tvdbTitles = new List<string>
                {
                    seriesSearchResult.SeriesName
                };
                tvdbTitles.AddRange(seriesSearchResult.Aliases);

                DateTime? firstAired = null;
                if (DateTime.TryParse(seriesSearchResult.FirstAired, out var parsedFirstAired))
                {
                    firstAired = parsedFirstAired;
                }

                var remoteSearchResult = new RemoteSearchResult
                {
                    Name = tvdbTitles.FirstOrDefault(),
                    ProductionYear = firstAired?.Year,
                    SearchProviderName = Name
                };

                if (!string.IsNullOrEmpty(seriesSearchResult.Poster))
                {
                    // Results from their Search endpoints already include the /banners/ part in the url, because reasons...
                    remoteSearchResult.ImageUrl = TvdbUtils.TvdbBaseUrl + seriesSearchResult.Poster.TrimStart('/');
                }

                try
                {
                    var seriesSesult =
                        await _tvdbClientManager.GetSeriesByIdAsync(seriesSearchResult.Id, language, cancellationToken)
                            .ConfigureAwait(false);
                    remoteSearchResult.SetProviderId(MetadataProvider.Imdb, seriesSesult.Data.ImdbId);
                    remoteSearchResult.SetProviderId(MetadataProvider.Zap2It, seriesSesult.Data.Zap2itId);
                }
                catch (TvDbServerException e)
                {
                    _logger.LogError(e, "Unable to retrieve series with id {TvdbId}:{SeriesName}", seriesSearchResult.Id, seriesSearchResult.SeriesName);
                }

                remoteSearchResult.SetProviderId(TvdbPlugin.ProviderId, seriesSearchResult.Id.ToString(CultureInfo.InvariantCulture));
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

        private static void MapActorsToResult(MetadataResult<Series> result, IEnumerable<Actor> actors)
        {
            foreach (Actor actor in actors)
            {
                var personInfo = new PersonInfo
                {
                    Type = PersonType.Actor,
                    Name = (actor.Name ?? string.Empty).Trim(),
                    Role = actor.Role,
                    SortOrder = actor.SortOrder
                };

                if (!string.IsNullOrEmpty(actor.Image))
                {
                    personInfo.ImageUrl = TvdbUtils.BannerUrl + actor.Image;
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

        private async Task MapSeriesToResult(MetadataResult<Series> result, SeriesExtendedRecordDto tvdbSeries, string metadataLanguage)
        {
            Series series = result.Item;
            series.SetProviderId(TvdbPlugin.ProviderId, tvdbSeries.Id.ToString(CultureInfo.InvariantCulture));
            series.Name = tvdbSeries.Name;
            series.Overview = (tvdbSeries.Overview ?? string.Empty).Trim();
            result.ResultLanguage = metadataLanguage;
            series.AirDays = TVUtils.GetAirDays(tvdbSeries.AirsDays.ToString());
            series.AirTime = tvdbSeries.AirsTime;
            series.CommunityRating = (float?)tvdbSeries.SiteRating;
            series.SetProviderId(MetadataProvider.Imdb, tvdbSeries.ImdbId);
            series.SetProviderId(MetadataProvider.Zap2It, tvdbSeries.Zap2itId);
            if (Enum.TryParse(tvdbSeries.Status, true, out SeriesStatus seriesStatus))
            {
                series.Status = seriesStatus;
            }

            if (DateTime.TryParse(tvdbSeries.FirstAired, out var date))
            {
                // dates from tvdb are UTC but without offset or Z
                series.PremiereDate = date;
                series.ProductionYear = date.Year;
            }

            if (!string.IsNullOrEmpty(tvdbSeries.Runtime) && double.TryParse(tvdbSeries.Runtime, out double runtime))
            {
                series.RunTimeTicks = TimeSpan.FromMinutes(runtime).Ticks;
            }

            foreach (var genre in tvdbSeries.Genre)
            {
                series.AddGenre(genre);
            }

            if (!string.IsNullOrEmpty(tvdbSeries.Network))
            {
                series.AddStudio(tvdbSeries.Network);
            }

            if (result.Item.Status.HasValue && result.Item.Status.Value == SeriesStatus.Ended)
            {
                try
                {
                    var episodeSummary = await _tvdbClientManager.GetSeriesEpisodeSummaryAsync(tvdbSeries.Id, metadataLanguage, CancellationToken.None).ConfigureAwait(false);

                    if (episodeSummary.Data.AiredSeasons.Length != 0)
                    {
                        var maxSeasonNumber = episodeSummary.Data.AiredSeasons.Max(s => Convert.ToInt32(s, CultureInfo.InvariantCulture));
                        var episodeQuery = new EpisodeQuery
                        {
                            AiredSeason = maxSeasonNumber
                        };
                        var episodesPage = await _tvdbClientManager.GetEpisodesPageAsync(tvdbSeries.Id, episodeQuery, metadataLanguage, CancellationToken.None).ConfigureAwait(false);

                        result.Item.EndDate = episodesPage.Data
                            .Select(e => DateTime.TryParse(e.FirstAired, out var firstAired) ? firstAired : (DateTime?)null)
                            .Max();
                    }
                }
                catch (TvDbServerException e)
                {
                    _logger.LogError(e, "Failed to find series end date for series {TvdbId}:{SeriesName}", tvdbSeries.Id, tvdbSeries?.SeriesName ?? result.Item?.Name);
                }
            }
        }
    }
}
