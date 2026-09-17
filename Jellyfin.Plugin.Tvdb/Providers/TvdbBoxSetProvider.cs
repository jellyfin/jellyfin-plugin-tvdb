using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Tvdb.Sdk;

namespace Jellyfin.Plugin.Tvdb.Providers
{
    /// <summary>
    /// The TvdbBoxSetProvider class.
    /// </summary>
    public class TvdbBoxSetProvider : IRemoteMetadataProvider<BoxSet, BoxSetInfo>
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TvdbEpisodeProvider> _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly TvdbClientManager _tvdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TvdbBoxSetProvider"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Instance of <see cref="IHttpClientFactory"/>.</param>
        /// <param name="logger">Instance of <see cref="ILogger{TvdbEpisodeProvider}"/>.</param>
        /// <param name="libraryManager">Instance of <see cref="ILibraryManager"/>.</param>
        /// <param name="tvdbClientManager">Instance of <see cref="TvdbClientManager"/>.</param>
        public TvdbBoxSetProvider(IHttpClientFactory httpClientFactory, ILogger<TvdbEpisodeProvider> logger, ILibraryManager libraryManager, TvdbClientManager tvdbClientManager)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _libraryManager = libraryManager;
            _tvdbClientManager = tvdbClientManager;
        }

        /// <inheritdoc />
        public string Name => TvdbPlugin.ProviderName;

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(BoxSetInfo searchInfo, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(searchInfo.Name))
            {
                // Search for box sets
                return await FindBoxSet(searchInfo.Name, searchInfo.MetadataLanguage, cancellationToken).ConfigureAwait(false);
            }

            // Return empty list if no results found
            return Enumerable.Empty<RemoteSearchResult>();
        }

        /// <inheritdoc />
        public async Task<MetadataResult<BoxSet>> GetMetadata(BoxSetInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<BoxSet>
            {
                QueriedById = true,
            };
            // Tvdb Box sets are only supported by tvdb ids
            if (!info.HasTvdbId())
            {
                result.QueriedById = false;
                await Identify(info).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();

            if (info.HasTvdbId())
            {
                result.Item = new BoxSet();
                result.HasMetadata = true;

                await FetchBoxSetMetadata(result, info, cancellationToken)
                    .ConfigureAwait(false);
            }

            return result;
        }

        private async Task<IEnumerable<RemoteSearchResult>> FindBoxSet(string name, string language, CancellationToken cancellationToken)
        {
            _logger.LogDebug("TvdbSearch: Finding id for item: {Name}", name);
            var results = await FindBoxSetsInternal(name, language, cancellationToken).ConfigureAwait(false);

            return results;
        }

        private async Task<List<RemoteSearchResult>> FindBoxSetsInternal(string name, string language, CancellationToken cancellationToken)
        {
            var parsedName = _libraryManager.ParseName(name);
            var comparableName = TvdbUtils.GetComparableName(parsedName.Name);

            var list = new List<Tuple<List<string>, RemoteSearchResult>>();
            IReadOnlyList<SearchResult> result;
            try
            {
                result = await _tvdbClientManager.GetBoxSetByNameAsync(comparableName, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "No BoxSet results found for {Name}", comparableName);
                return new List<RemoteSearchResult>();
            }

            foreach (var boxSetSearchResult in result)
            {
                var tvdbTitles = new List<string>
                {
                    boxSetSearchResult.Translations.GetTranslatedNamedOrDefault(language) ?? boxSetSearchResult.Name
                };
                if (boxSetSearchResult.Aliases is not null)
                {
                    tvdbTitles.AddRange(boxSetSearchResult.Aliases);
                }

                var remoteSearchResult = new RemoteSearchResult
                {
                    Name = tvdbTitles.FirstOrDefault(),
                    SearchProviderName = Name
                };

                if (!string.IsNullOrEmpty(boxSetSearchResult.Image_url))
                {
                    remoteSearchResult.ImageUrl = boxSetSearchResult.Image_url;
                }

                remoteSearchResult.SetTvdbId(boxSetSearchResult.Tvdb_id);
                list.Add(new Tuple<List<string>, RemoteSearchResult>(tvdbTitles, remoteSearchResult));
            }

            return list
                .OrderBy(i => i.Item1.Contains(name, StringComparer.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(i => i.Item1.Any(title => title.Contains(parsedName.Name, StringComparison.OrdinalIgnoreCase)) ? 0 : 1)
                .ThenBy(i => i.Item1.Any(title => title.Contains(comparableName, StringComparison.OrdinalIgnoreCase)) ? 0 : 1)
                .ThenBy(i => list.IndexOf(i))
                .Select(i => i.Item2)
                .ToList();
        }

        private async Task Identify(BoxSetInfo info)
        {
            if (info.HasTvdbId())
            {
                return;
            }

            var remoteSearchResults = await FindBoxSet(info.Name, info.MetadataLanguage, CancellationToken.None)
                .ConfigureAwait(false);

            var entry = remoteSearchResults.FirstOrDefault();
            if (entry.HasTvdbId(out var tvdbId))
            {
                info.SetTvdbId(tvdbId);
            }
        }

        private async Task FetchBoxSetMetadata(
            MetadataResult<BoxSet> result,
            BoxSetInfo boxSetInfo,
            CancellationToken cancellationToken)
        {
            var boxSetMetadata = result.Item;

            if (boxSetInfo.HasTvdbId(out var tvdbIdTxt))
            {
                boxSetMetadata.SetTvdbId(tvdbIdTxt);
            }

            if (string.IsNullOrWhiteSpace(tvdbIdTxt))
            {
                _logger.LogWarning("No valid tvdb id found for BoxSet {TvdbId}:{BoxSetName}", tvdbIdTxt, boxSetInfo.Name);
                return;
            }

            var tvdbId = Convert.ToInt32(tvdbIdTxt, CultureInfo.InvariantCulture);
            try
            {
                var boxSetResult =
                    await _tvdbClientManager
                        .GetBoxSetExtendedByIdAsync(tvdbId, cancellationToken)
                        .ConfigureAwait(false);
                MapBoxSetToResult(result, boxSetResult, boxSetInfo);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to retrieve BoxSet with id {TvdbId}:{BoxSetName}", tvdbId, boxSetInfo.Name);
                return;
            }
        }

        private void MapBoxSetToResult(MetadataResult<BoxSet> result, ListExtendedRecord tvdbBoxSet, BoxSetInfo info)
        {
            BoxSet boxSet = result.Item;
            boxSet.SetTvdbId(tvdbBoxSet.Id);
            // Tvdb uses 3 letter code for language (prob ISO 639-2)
            // Reverts to OriginalName if no translation is found
            // boxSet.Name = tvdbBoxSet.Translations.GetTranslatedNamedOrDefault(info.MetadataLanguage) ?? TvdbUtils.ReturnOriginalLanguageOrDefault(tvdbMovie.Name);
            boxSet.Name = tvdbBoxSet.Name;
            // boxSet.Overview = tvdbBoxSet.Translations.GetTranslatedOverviewOrDefault(info.MetadataLanguage);
            boxSet.Overview = tvdbBoxSet.Overview;
            boxSet.OriginalTitle = tvdbBoxSet.Name;
            result.ResultLanguage = info.MetadataLanguage;
            // Attempts to default to USA if not found
            boxSet.SetProviderIdIfHasValue(TvdbPlugin.SlugProviderId, tvdbBoxSet.Url);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);
        }
    }
}
