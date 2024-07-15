using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Tvdb.Sdk;

namespace Jellyfin.Plugin.Tvdb.Providers
{
    /// <summary>
    /// The TVDB movie provider.
    /// </summary>
    public class TvdbMovieProvider : IRemoteMetadataProvider<Movie, MovieInfo>
    {
        private const int MaxSearchResults = 10;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TvdbMovieProvider> _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly TvdbClientManager _tvdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TvdbMovieProvider"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Instance of <see cref="IHttpClientFactory"/>.</param>
        /// <param name="logger">Instance of <see cref="ILogger{TvdbMovieProvider}"/>.</param>
        /// <param name="libraryManager">Instance of <see cref="ILibraryManager"/>.</param>
        /// <param name="tvdbClientManager">Instance of <see cref="TvdbClientManager"/>.</param>
        public TvdbMovieProvider(
            IHttpClientFactory httpClientFactory,
            ILogger<TvdbMovieProvider> logger,
            ILibraryManager libraryManager,
            TvdbClientManager tvdbClientManager)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _libraryManager = libraryManager;
            _tvdbClientManager = tvdbClientManager;
        }

        /// <inheritdoc/>
        public string Name => TvdbPlugin.ProviderName;

        /// <inheritdoc/>
        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
        {
            if (searchInfo.IsSupported())
            {
                return await FetchMovieSearchResult(searchInfo, cancellationToken).ConfigureAwait(false);
            }

            return await FindMovie(searchInfo.Name, searchInfo.Year, searchInfo.MetadataLanguage, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            throw new System.NotImplementedException();
        }

        private async Task<IEnumerable<RemoteSearchResult>> FetchMovieSearchResult(MovieInfo seriesInfo, CancellationToken cancellationToken)
        {
            async Task<string?> TryGetTvdbIdWithRemoteId(MetadataProvider metadataProvider)
            {
                var id = seriesInfo.GetProviderId(metadataProvider);
                if (string.IsNullOrEmpty(id))
                {
                    return null;
                }

                return await GetMovieByRemoteId(
                    id,
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
                var movieResult =
                    await _tvdbClientManager
                        .GetMovieExtendedByIdAsync(tvdbId.Value, cancellationToken)
                        .ConfigureAwait(false);
                return new[] { MapMovieToRemoteSearchResult(movieResult, seriesInfo.MetadataLanguage) };
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to retrieve series with id {TvdbId}:{SeriesName}", tvdbId, seriesInfo.Name);
                return Array.Empty<RemoteSearchResult>();
            }
        }

        private RemoteSearchResult MapMovieToRemoteSearchResult(MovieExtendedRecord movie, string language)
        {
            var remoteResult = new RemoteSearchResult
            {
                Name = movie.Translations.GetTranslatedNamedOrDefault(language) ?? TvdbUtils.ReturnOriginalLanguageOrDefault(movie.Name),
                Overview = movie.Translations.GetTranslatedOverviewOrDefault(language)?.Trim(),
                SearchProviderName = Name,
                ImageUrl = movie.Image
            };

            if (DateTime.TryParse(movie.First_release.Date, out var date))
            {
                // Dates from tvdb are either EST or capital of primary airing country.
                remoteResult.PremiereDate = date;
                remoteResult.ProductionYear = date.Year;
            }

            var imdbID = movie.RemoteIds?.FirstOrDefault(x => string.Equals(x.SourceName, "IMDB", StringComparison.OrdinalIgnoreCase))?.Id;
            remoteResult.SetProviderIdIfHasValue(MetadataProvider.Imdb, imdbID);
            remoteResult.SetTvdbId(movie.Id);

            return remoteResult;
        }

        private async Task<string?> GetMovieByRemoteId(string remoteId, CancellationToken cancellationToken)
        {
            IReadOnlyList<SearchByRemoteIdResult> resultData;
            try
            {
                resultData = await _tvdbClientManager.GetMovieByRemoteIdAsync(remoteId, cancellationToken)
                            .ConfigureAwait(false);
            }
            catch (SearchException ex) when (ex.InnerException is JsonException)
            {
                _logger.LogError(ex, "Failed to retrieve series with {RemoteId}", remoteId);
                return null;
            }

            if (resultData is null || resultData.Count == 0 || resultData[0]?.Movie?.Id is null)
            {
                _logger.LogWarning("TvdbSearch: No series found for remote id: {RemoteId}", remoteId);
                return null;
            }

            return resultData[0].Movie.Id?.ToString(CultureInfo.InvariantCulture);
        }

        private async Task<IEnumerable<RemoteSearchResult>> FindMovie(string name, int? year, string language, CancellationToken cancellationToken)
        {
            _logger.LogDebug("TvdbSearch: Finding id for item: {Name} ({Year})", name, year);
            var results = await FindMovieInternal(name, language, cancellationToken).ConfigureAwait(false);

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

        private async Task<List<RemoteSearchResult>> FindMovieInternal(string name, string language, CancellationToken cancellationToken)
        {
            var parsedName = _libraryManager.ParseName(name);
            var comparableName = TvdbUtils.GetComparableName(parsedName.Name);

            var list = new List<Tuple<List<string>, RemoteSearchResult>>();
            IReadOnlyList<SearchResult> result;
            try
            {
                result = await _tvdbClientManager.GetMovieByNameAsync(comparableName, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "No series results found for {Name}", comparableName);
                return new List<RemoteSearchResult>();
            }

            foreach (var movieSearchResult in result)
            {
                var tvdbTitles = new List<string>
                {
                    movieSearchResult.Translations.GetTranslatedNamedOrDefault(language) ?? movieSearchResult.Name
                };
                if (movieSearchResult.Aliases is not null)
                {
                    tvdbTitles.AddRange(movieSearchResult.Aliases);
                }

                DateTime? firstAired = null;
                if (DateTime.TryParse(movieSearchResult.First_air_time, out var parsedFirstAired))
                {
                    firstAired = parsedFirstAired;
                }

                var remoteSearchResult = new RemoteSearchResult
                {
                    Name = tvdbTitles.FirstOrDefault(),
                    ProductionYear = firstAired?.Year,
                    SearchProviderName = Name
                };

                if (!string.IsNullOrEmpty(movieSearchResult.Image_url))
                {
                    remoteSearchResult.ImageUrl = movieSearchResult.Image_url;
                }

                try
                {
                    var movieResult =
                        await _tvdbClientManager.GetMovieExtendedByIdAsync(Convert.ToInt32(movieSearchResult.Tvdb_id, CultureInfo.InvariantCulture), cancellationToken)
                            .ConfigureAwait(false);

                    var imdbId = movieResult.RemoteIds?.FirstOrDefault(x => string.Equals(x.SourceName, "IMDB", StringComparison.OrdinalIgnoreCase))?.Id.ToString();
                    remoteSearchResult.SetProviderIdIfHasValue(MetadataProvider.Imdb, imdbId);

                    var zap2ItId = movieResult.RemoteIds?.FirstOrDefault(x => string.Equals(x.SourceName, "Zap2It", StringComparison.OrdinalIgnoreCase))?.Id.ToString();
                    remoteSearchResult.SetProviderIdIfHasValue(MetadataProvider.Zap2It, zap2ItId);

                    var tmdbId = movieResult.RemoteIds?.FirstOrDefault(x => string.Equals(x.SourceName, "TheMovieDB.com", StringComparison.OrdinalIgnoreCase))?.Id.ToString();

                    // Sometimes, tvdb will return tmdbid as {tmdbid}-{title} like in the tmdb url. Grab the tmdbid only.
                    var tmdbIdLeft = StringExtensions.LeftPart(tmdbId, '-').ToString();
                    remoteSearchResult.SetProviderIdIfHasValue(MetadataProvider.Tmdb, tmdbIdLeft);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unable to retrieve series with id {TvdbId}:{SeriesName}", movieSearchResult.Tvdb_id, movieSearchResult.Name);
                }

                remoteSearchResult.SetTvdbId(movieSearchResult.Tvdb_id);
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

        /// <inheritdoc/>
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);
        }
    }
}
