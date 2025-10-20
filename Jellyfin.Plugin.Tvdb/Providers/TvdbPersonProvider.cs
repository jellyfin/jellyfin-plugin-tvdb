using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Tvdb.Sdk;

namespace Jellyfin.Plugin.Tvdb.Providers
{
    /// <summary>
    /// Tvdb person provider.
    /// </summary>
    public class TvdbPersonProvider : IRemoteMetadataProvider<Person, PersonLookupInfo>
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TvdbPersonProvider> _logger;
        private readonly TvdbClientManager _tvdbClientManager;
        private readonly ILibraryManager _libraryManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TvdbPersonProvider"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger{TvdbPersonProvider}"/> interface.</param>
        /// <param name="tvdbClientManager">Instance of <see cref="TvdbClientManager"/>.</param>
        /// <param name="libraryManager">Instance of <see cref="ILibraryManager"/>.</param>
        public TvdbPersonProvider(
            IHttpClientFactory httpClientFactory,
            ILogger<TvdbPersonProvider> logger,
            TvdbClientManager tvdbClientManager,
            ILibraryManager libraryManager)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _tvdbClientManager = tvdbClientManager;
            _libraryManager = libraryManager;
        }

        /// <inheritdoc />
        public string Name => TvdbPlugin.ProviderName;

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(PersonLookupInfo searchInfo, CancellationToken cancellationToken)
        {
            if (searchInfo.IsSupported())
            {
                return await FetchPeopleSearchResult(searchInfo, cancellationToken).ConfigureAwait(false);
            }

            return await FindPerson(searchInfo.Name, searchInfo.MetadataLanguage, cancellationToken).ConfigureAwait(false);
        }

        /// <inheritdoc />
        public async Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Person>
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
                result.Item = new Person();
                result.HasMetadata = true;

                await FetchPeopleMetadata(result, info, cancellationToken)
                    .ConfigureAwait(false);
            }

            return result;
        }

        private async Task<IEnumerable<RemoteSearchResult>> FetchPeopleSearchResult(PersonLookupInfo personInfo, CancellationToken cancellationToken)
        {
            async Task<string?> TryGetTvdbIdWithRemoteId(MetadataProvider metadataProvider)
            {
                var id = personInfo.GetProviderId(metadataProvider);
                if (string.IsNullOrEmpty(id))
                {
                    return null;
                }

                return await GetPersonByRemoteId(
                    id,
                    cancellationToken).ConfigureAwait(false);
            }

            int? tvdbId;
            if (personInfo.HasTvdbId())
            {
                tvdbId = personInfo.GetTvdbId();
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
                _logger.LogWarning("No valid tvdb id found for Person {TvdbId}:{PersonName}", tvdbId, personInfo.Name);
                return Array.Empty<RemoteSearchResult>();
            }

            try
            {
                var peopleResult =
                    await _tvdbClientManager
                        .GetActorExtendedByIdAsync(tvdbId.Value, cancellationToken)
                        .ConfigureAwait(false);
                return new[] { MapPeopleToRemoteSearchResult(peopleResult, personInfo.MetadataLanguage) };
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to retrieve person with id {TvdbId}:{PersonName}", tvdbId, personInfo.Name);
                return Array.Empty<RemoteSearchResult>();
            }
        }

        private RemoteSearchResult MapPeopleToRemoteSearchResult(PeopleExtendedRecord person, string language)
        {
            var remoteResult = new RemoteSearchResult
            {
                Name = person.Translations.GetTranslatedNamedOrDefault(language) ?? TvdbUtils.ReturnOriginalLanguageOrDefault(person.Name),
                Overview = person.Translations.GetTranslatedOverviewOrDefault(language)?.Trim(),
                SearchProviderName = Name,
                ImageUrl = person.Image
            };

            var imdbID = person.RemoteIds?.FirstOrDefault(x => string.Equals(x.SourceName, "IMDB", StringComparison.OrdinalIgnoreCase))?.Id;
            remoteResult.SetProviderIdIfHasValue(MetadataProvider.Imdb, imdbID);
            remoteResult.SetTvdbId(person.Id);

            return remoteResult;
        }

        private async Task<string?> GetPersonByRemoteId(string remoteId, CancellationToken cancellationToken)
        {
            IReadOnlyList<SearchByRemoteIdResult> resultData;
            try
            {
                resultData = await _tvdbClientManager.GetActorByRemoteIdAsync(remoteId, cancellationToken)
                            .ConfigureAwait(false);
            }
            catch (SearchException ex) when (ex.InnerException is JsonException)
            {
                _logger.LogError(ex, "Failed to retrieve person with {RemoteId}", remoteId);
                return null;
            }

            if (resultData is null || resultData.Count == 0 || resultData[0]?.People?.Id is null)
            {
                _logger.LogWarning("TvdbSearch: No person found for remote id: {RemoteId}", remoteId);
                return null;
            }

            return resultData[0].People.Id?.ToString(CultureInfo.InvariantCulture);
        }

        private async Task<IEnumerable<RemoteSearchResult>> FindPerson(string name, string language, CancellationToken cancellationToken)
        {
            _logger.LogDebug("TvdbSearch: Finding id for item: {Name}", name);
            return await FindPersonInternal(name, language, cancellationToken).ConfigureAwait(false);
        }

        private async Task<List<RemoteSearchResult>> FindPersonInternal(string name, string language, CancellationToken cancellationToken)
        {
            var parsedName = _libraryManager.ParseName(name);
            var comparableName = TvdbUtils.GetComparableName(parsedName.Name);

            var list = new List<(List<string> Titles, RemoteSearchResult SearchResult)>();
            IReadOnlyList<SearchResult> result;
            try
            {
                result = await _tvdbClientManager.GetActorByNameAsync(comparableName, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "No person results found for {Name}", comparableName);
                return new List<RemoteSearchResult>();
            }

            foreach (var peopleSearchResult in result)
            {
                var tvdbTitles = new List<string>
                {
                    peopleSearchResult.Translations.GetTranslatedNamedOrDefault(language) ?? peopleSearchResult.Name
                };
                if (peopleSearchResult.Aliases is not null)
                {
                    tvdbTitles.AddRange(peopleSearchResult.Aliases);
                }

                var remoteSearchResult = new RemoteSearchResult
                {
                    Name = tvdbTitles.FirstOrDefault(),
                    SearchProviderName = Name
                };

                if (!string.IsNullOrEmpty(peopleSearchResult.Image_url))
                {
                    remoteSearchResult.ImageUrl = peopleSearchResult.Image_url;
                }

                try
                {
                    var peopleResult =
                        await _tvdbClientManager.GetActorExtendedByIdAsync(Convert.ToInt32(peopleSearchResult.Tvdb_id, CultureInfo.InvariantCulture), cancellationToken)
                            .ConfigureAwait(false);

                    var imdbId = peopleResult.RemoteIds?.FirstOrDefault(x => string.Equals(x.SourceName, "IMDB", StringComparison.OrdinalIgnoreCase))?.Id.ToString();
                    remoteSearchResult.SetProviderIdIfHasValue(MetadataProvider.Imdb, imdbId);

                    var zap2ItId = peopleResult.RemoteIds?.FirstOrDefault(x => string.Equals(x.SourceName, "Zap2It", StringComparison.OrdinalIgnoreCase))?.Id.ToString();
                    remoteSearchResult.SetProviderIdIfHasValue(MetadataProvider.Zap2It, zap2ItId);

                    var tmdbId = peopleResult.RemoteIds?.FirstOrDefault(x => string.Equals(x.SourceName, "TheMovieDB.com", StringComparison.OrdinalIgnoreCase))?.Id.ToString();

                    // Sometimes, tvdb will return tmdbid as {tmdbid}-{title} like in the tmdb url. Grab the tmdbid only.
                    var tmdbIdLeft = StringExtensions.LeftPart(tmdbId, '-').ToString();
                    remoteSearchResult.SetProviderIdIfHasValue(MetadataProvider.Tmdb, tmdbIdLeft);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Unable to retrieve person with id {TvdbId}:{PersonName}", peopleSearchResult.Tvdb_id, peopleSearchResult.Name);
                }

                remoteSearchResult.SetTvdbId(peopleSearchResult.Tvdb_id);
                list.Add((tvdbTitles, remoteSearchResult));
            }

            return list
                .OrderBy(i => i.Titles.Contains(name, StringComparer.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(i => i.Titles.Any(title => title.Contains(parsedName.Name, StringComparison.OrdinalIgnoreCase)) ? 0 : 1)
                .ThenBy(i => i.Titles.Any(title => title.Contains(comparableName, StringComparison.OrdinalIgnoreCase)) ? 0 : 1)
                .ThenBy(i => list.IndexOf(i))
                .Select(i => i.SearchResult)
                .ToList();
        }

        private async Task Identify(PersonLookupInfo info)
        {
            if (info.HasTvdbId())
            {
                return;
            }

            var remoteSearchResults = await FindPerson(info.Name, info.MetadataLanguage, CancellationToken.None)
                .ConfigureAwait(false);

            var entry = remoteSearchResults.FirstOrDefault();
            if (entry.HasTvdbId(out var tvdbId))
            {
                info.SetTvdbId(tvdbId);
            }
        }

        private async Task FetchPeopleMetadata(
            MetadataResult<Person> result,
            PersonLookupInfo personInfo,
            CancellationToken cancellationToken)
        {
            var personMetadata = result.Item;
            async Task<string?> TryGetTvdbIdWithRemoteId(string id)
            {
                return await GetPersonByRemoteId(
                    id,
                    cancellationToken).ConfigureAwait(false);
            }

            if (personInfo.HasTvdbId(out var tvdbIdTxt))
            {
                personMetadata.SetTvdbId(tvdbIdTxt);
            }

            if (personInfo.HasProviderId(MetadataProvider.Imdb, out var imdbId))
            {
                personMetadata.SetProviderId(MetadataProvider.Imdb, imdbId!);
                tvdbIdTxt ??= await TryGetTvdbIdWithRemoteId(imdbId!).ConfigureAwait(false);
            }

            if (personInfo.HasProviderId(MetadataProvider.Zap2It, out var zap2It))
            {
                personMetadata.SetProviderId(MetadataProvider.Zap2It, zap2It!);
                tvdbIdTxt ??= await TryGetTvdbIdWithRemoteId(zap2It!).ConfigureAwait(false);
            }

            if (personInfo.HasProviderId(MetadataProvider.Tmdb, out var tmdbId))
            {
                personMetadata.SetProviderId(MetadataProvider.Tmdb, tmdbId!);
                tvdbIdTxt ??= await TryGetTvdbIdWithRemoteId(tmdbId!).ConfigureAwait(false);
            }

            if (string.IsNullOrWhiteSpace(tvdbIdTxt))
            {
                _logger.LogWarning("No valid tvdb id found for person {TvdbId}:{PersonName}", tvdbIdTxt, personInfo.Name);
                return;
            }

            var tvdbId = Convert.ToInt32(tvdbIdTxt, CultureInfo.InvariantCulture);
            try
            {
                var personResult =
                    await _tvdbClientManager
                        .GetActorExtendedByIdAsync(tvdbId, cancellationToken)
                        .ConfigureAwait(false);
                MapPersonToResult(result, personResult, personInfo);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to retrieve person with id {TvdbId}:{PersonName}", tvdbId, personInfo.Name);
                return;
            }
        }

        private void MapPersonToResult(MetadataResult<Person> result, PeopleExtendedRecord tvdbPerson, PersonLookupInfo info)
        {
            Person person = result.Item;
            person.SetTvdbId(tvdbPerson.Id);
            // Tvdb uses 3 letter code for language (prob ISO 639-2)
            // Reverts to OriginalName if no translation is found
            person.Name = tvdbPerson.Translations.GetTranslatedNamedOrDefault(info.MetadataLanguage) ?? TvdbUtils.ReturnOriginalLanguageOrDefault(tvdbPerson.Name);
            person.Overview = tvdbPerson.Translations.GetTranslatedOverviewOrDefault(info.MetadataLanguage);
            result.ResultLanguage = info.MetadataLanguage;

            var imdbId = tvdbPerson.RemoteIds?.FirstOrDefault(x => string.Equals(x.SourceName, "IMDB", StringComparison.OrdinalIgnoreCase))?.Id.ToString();
            person.SetProviderIdIfHasValue(MetadataProvider.Imdb, imdbId);

            var zap2ItId = tvdbPerson.RemoteIds?.FirstOrDefault(x => string.Equals(x.SourceName, "Zap2It", StringComparison.OrdinalIgnoreCase))?.Id.ToString();
            person.SetProviderIdIfHasValue(MetadataProvider.Zap2It, zap2ItId);

            var tmdbId = tvdbPerson.RemoteIds?.FirstOrDefault(x => string.Equals(x.SourceName, "TheMovieDB.com", StringComparison.OrdinalIgnoreCase))?.Id.ToString();
            // Sometimes, tvdb will return tmdbid as {tmdbid}-{title} like in the tmdb url. Grab the tmdbid only.
            var tmdbIdLeft = StringExtensions.LeftPart(tmdbId, '-').ToString();
            person.SetProviderIdIfHasValue(MetadataProvider.Tmdb, tmdbIdLeft);

            if (!string.IsNullOrWhiteSpace(tvdbPerson.BirthPlace))
            {
                person.ProductionLocations = new[] { tvdbPerson.BirthPlace };
            }

            if (!string.IsNullOrEmpty(tvdbPerson.Birth) &&
                DateTime.TryParse(tvdbPerson.Birth, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var birthDate))
            {
                person.PremiereDate = birthDate.ToUniversalTime();
            }

            if (!string.IsNullOrEmpty(tvdbPerson.Death) &&
                DateTime.TryParse(tvdbPerson.Death, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var deathDate))
            {
                person.EndDate = deathDate.ToUniversalTime();
            }
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);
        }
    }
}
