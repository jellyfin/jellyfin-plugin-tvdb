using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Events;
using MediaBrowser.Controller.BaseItemManager;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using Microsoft.Extensions.Logging;
using TvDbSharper;
using TvDbSharper.Dto;
using Series = MediaBrowser.Controller.Entities.TV.Series;

namespace Jellyfin.Plugin.Tvdb.Providers
{
    /// <summary>
    /// Tvdb Missing Episode provider.
    /// </summary>
    public class TvdbMissingEpisodeProvider : IServerEntryPoint
    {
        /// <summary>
        /// The provider name.
        /// </summary>
        public static readonly string ProviderName = "Missing Episode Fetcher";

        private readonly TvdbClientManager _tvdbClientManager;
        private readonly IBaseItemManager _baseItemManager;
        private readonly IProviderManager _providerManager;
        private readonly ILocalizationManager _localization;
        private readonly ILibraryManager _libraryManager;
        private readonly ILogger<TvdbMissingEpisodeProvider> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TvdbMissingEpisodeProvider"/> class.
        /// </summary>
        /// <param name="tvdbClientManager">Instance of the <see cref="TvdbClientManager"/> class.</param>
        /// <param name="baseItemManager">Instance of the <see cref="IBaseItemManager"/> interface.</param>
        /// <param name="providerManager">Instance of the <see cref="IProviderManager"/> interface.</param>
        /// <param name="localization">Instance of the <see cref="ILocalizationManager"/> interface.</param>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger{TvdbMissingEpisodeProvider}"/> interface.</param>
        public TvdbMissingEpisodeProvider(
            TvdbClientManager tvdbClientManager,
            IBaseItemManager baseItemManager,
            IProviderManager providerManager,
            ILocalizationManager localization,
            ILibraryManager libraryManager,
            ILogger<TvdbMissingEpisodeProvider> logger)
        {
            _tvdbClientManager = tvdbClientManager;
            _baseItemManager = baseItemManager;
            _providerManager = providerManager;
            _localization = localization;
            _libraryManager = libraryManager;
            _logger = logger;
        }

        /// <inheritdoc />
        public Task RunAsync()
        {
            _providerManager.RefreshCompleted += OnProviderManagerRefreshComplete;
            _libraryManager.ItemUpdated += OnLibraryManagerItemUpdated;
            _libraryManager.ItemRemoved += OnLibraryManagerItemRemoved;

            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes managed resources.
        /// </summary>
        /// <param name="disposing">A value indicating whether managed resources should be disposed.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _providerManager.RefreshCompleted -= OnProviderManagerRefreshComplete;
                _libraryManager.ItemUpdated -= OnLibraryManagerItemUpdated;
                _libraryManager.ItemRemoved -= OnLibraryManagerItemRemoved;
            }
        }

        private static bool IsValidEpisode(EpisodeRecord? episodeRecord)
        {
            return episodeRecord?.AiredSeason != null && episodeRecord.AiredEpisodeNumber != null;
        }

        private static bool EpisodeExists(EpisodeRecord episodeRecord, IReadOnlyList<Episode> existingEpisodes)
        {
            return existingEpisodes.Any(ep => ep.ContainsEpisodeNumber(episodeRecord.AiredEpisodeNumber!.Value) && ep.ParentIndexNumber == episodeRecord.AiredSeason);
        }

        private bool IsEnabledForLibrary(BaseItem item)
        {
            Series? series = item switch
            {
                Episode episode => episode.Series,
                Season season => season.Series,
                _ => item as Series
            };

            if (series == null)
            {
                return false;
            }

            var libraryOptions = _libraryManager.GetLibraryOptions(series);
            return _baseItemManager.IsMetadataFetcherEnabled(series, libraryOptions, ProviderName);
        }

        // TODO use the new async events when provider manager is updated
        private void OnProviderManagerRefreshComplete(object? sender, GenericEventArgs<BaseItem> genericEventArgs)
        {
            if (!IsEnabledForLibrary(genericEventArgs.Argument))
            {
                return;
            }

            if (genericEventArgs.Argument is Series series)
            {
                HandleSeries(series).GetAwaiter().GetResult();
            }

            if (genericEventArgs.Argument is Season season)
            {
                HandleSeason(season).GetAwaiter().GetResult();
            }
        }

        private async Task HandleSeries(Series series)
        {
            if (!series.TryGetProviderId(MetadataProvider.Tvdb.ToString(), out var tvdbIdTxt))
            {
                return;
            }

            var tvdbId = Convert.ToInt32(tvdbIdTxt, CultureInfo.InvariantCulture);

            var children = series.GetRecursiveChildren();
            var existingSeasons = new List<Season>();
            var existingEpisodes = new Dictionary<int, List<Episode>>();
            for (var i = 0; i < children.Count; i++)
            {
                switch (children[i])
                {
                    case Season season:
                        if (season.IndexNumber.HasValue)
                        {
                            existingSeasons.Add(season);
                        }

                        break;
                    case Episode episode:
                        var seasonNumber = episode.ParentIndexNumber ?? 1;
                        if (!existingEpisodes.ContainsKey(seasonNumber))
                        {
                            existingEpisodes[seasonNumber] = new List<Episode>();
                        }

                        existingEpisodes[seasonNumber].Add(episode);
                        break;
                }
            }

            var allEpisodes = await GetAllEpisodes(tvdbId, series.GetPreferredMetadataLanguage()).ConfigureAwait(false);
            var allSeasons = allEpisodes
                .Where(ep => ep.AiredSeason.HasValue)
                .Select(ep => ep.AiredSeason!.Value)
                .Distinct()
                .ToList();

            // Add missing seasons
            var newSeasons = AddMissingSeasons(series, existingSeasons, allSeasons);
            AddMissingEpisodes(existingEpisodes, allEpisodes, existingSeasons.Concat(newSeasons).ToList());
        }

        private async Task HandleSeason(Season season)
        {
            if (season.Series == null
                || !season.Series.TryGetProviderId(MetadataProvider.Tvdb.ToString(), out var tvdbIdTxt))
            {
                return;
            }

            var tvdbId = Convert.ToInt32(tvdbIdTxt, CultureInfo.InvariantCulture);

            var query = new EpisodeQuery
            {
                AiredSeason = season.IndexNumber
            };
            var allEpisodes = await GetAllEpisodes(tvdbId, season.GetPreferredMetadataLanguage(), query).ConfigureAwait(false);

            var existingEpisodes = season.Children.OfType<Episode>().ToList();

            for (var i = 0; i < allEpisodes.Count; i++)
            {
                var episode = allEpisodes[i];
                if (EpisodeExists(episode, existingEpisodes))
                {
                    continue;
                }

                AddVirtualEpisode(episode, season);
            }
        }

        private void OnLibraryManagerItemUpdated(object? sender, ItemChangeEventArgs itemChangeEventArgs)
        {
            // Only interested in real Season and Episode items
            if (itemChangeEventArgs.Item.IsVirtualItem
                || !(itemChangeEventArgs.Item is Season || itemChangeEventArgs.Item is Episode))
            {
                return;
            }

            if (!IsEnabledForLibrary(itemChangeEventArgs.Item))
            {
                return;
            }

            var indexNumber = itemChangeEventArgs.Item.IndexNumber;

            // If the item is an Episode, filter on ParentIndexNumber as well (season number)
            int? parentIndexNumber = null;
            if (itemChangeEventArgs.Item is Episode)
            {
                parentIndexNumber = itemChangeEventArgs.Item.ParentIndexNumber;
            }

            var query = new InternalItemsQuery
            {
                IsVirtualItem = true,
                IndexNumber = indexNumber,
                ParentIndexNumber = parentIndexNumber,
                IncludeItemTypes = new[] { itemChangeEventArgs.Item.GetBaseItemKind() },
                Parent = itemChangeEventArgs.Parent,
                GroupByPresentationUniqueKey = false,
                DtoOptions = new DtoOptions(true)
            };

            var existingVirtualItems = _libraryManager.GetItemList(query);

            var deleteOptions = new DeleteOptions
            {
                DeleteFileLocation = true
            };

            // Remove the virtual season/episode that matches the newly updated item
            for (var i = 0; i < existingVirtualItems.Count; i++)
            {
                _libraryManager.DeleteItem(existingVirtualItems[i], deleteOptions);
            }
        }

        // TODO use async events
        private void OnLibraryManagerItemRemoved(object? sender, ItemChangeEventArgs itemChangeEventArgs)
        {
            // No action needed if the item is virtual
            if (itemChangeEventArgs.Item.IsVirtualItem || !IsEnabledForLibrary(itemChangeEventArgs.Item))
            {
                return;
            }

            // Create a new virtual season if the real one was deleted.
            // Similarly, create a new virtual episode if the real one was deleted.
            if (itemChangeEventArgs.Item is Season season)
            {
                var newSeason = AddVirtualSeason(season.IndexNumber!.Value, season.Series);
                HandleSeason(newSeason).GetAwaiter().GetResult();
            }
            else if (itemChangeEventArgs.Item is Episode episode)
            {
                if (episode.Series == null
                    || !episode.Series.TryGetProviderId(MetadataProvider.Tvdb.ToString(), out var tvdbIdTxt))
                {
                    return;
                }

                var tvdbId = Convert.ToInt32(tvdbIdTxt, CultureInfo.InvariantCulture);

                var query = new EpisodeQuery
                {
                    AiredSeason = episode.ParentIndexNumber,
                    AiredEpisode = episode.IndexNumber
                };
                var episodeRecords = GetAllEpisodes(tvdbId, episode.GetPreferredMetadataLanguage(), query).GetAwaiter().GetResult();

                EpisodeRecord? episodeRecord = null;
                if (episodeRecords.Count > 0)
                {
                    episodeRecord = episodeRecords[0];
                }

                AddVirtualEpisode(episodeRecord, episode.Season);
            }
        }

        private async Task<IReadOnlyList<EpisodeRecord>> GetAllEpisodes(int tvdbId, string acceptedLanguage, EpisodeQuery? episodeQuery = null)
        {
            try
            {
                // Fetch all episodes for the series
                var allEpisodes = new List<EpisodeRecord>();
                var page = 1;
                while (true)
                {
                    episodeQuery ??= new EpisodeQuery();
                    var episodes = await _tvdbClientManager.GetEpisodesPageAsync(
                        tvdbId,
                        page,
                        episodeQuery,
                        acceptedLanguage,
                        CancellationToken.None).ConfigureAwait(false);

                    if (episodes.Data == null)
                    {
                        _logger.LogWarning("Unable to get episodes from TVDB: Episode Query returned null for TVDB Id: {TvdbId}", tvdbId);
                        return Array.Empty<EpisodeRecord>();
                    }

                    allEpisodes.AddRange(episodes.Data);
                    if (!episodes.Links.Next.HasValue)
                    {
                        break;
                    }

                    page = episodes.Links.Next.Value;
                }

                return allEpisodes;
            }
            catch (TvDbServerException ex)
            {
                _logger.LogWarning(ex, "Unable to get episodes from TVDB");
                return Array.Empty<EpisodeRecord>();
            }
        }

        private IEnumerable<Season> AddMissingSeasons(Series series, List<Season> existingSeasons, IReadOnlyList<int> allSeasons)
        {
            var missingSeasons = allSeasons.Except(existingSeasons.Select(s => s.IndexNumber!.Value)).ToList();
            for (var i = 0; i < missingSeasons.Count; i++)
            {
                var season = missingSeasons[i];
                yield return AddVirtualSeason(season, series);
            }
        }

        private void AddMissingEpisodes(
            Dictionary<int, List<Episode>> existingEpisodes,
            IReadOnlyList<EpisodeRecord> allEpisodeRecords,
            IReadOnlyList<Season> existingSeasons)
        {
            for (var i = 0; i < allEpisodeRecords.Count; i++)
            {
                var episodeRecord = allEpisodeRecords[i];
                // tvdb has a lot of bad data?
                if (!IsValidEpisode(episodeRecord))
                {
                    continue;
                }

                // skip if it exists already
                if (existingEpisodes.TryGetValue(episodeRecord.AiredSeason!.Value, out var episodes)
                    && EpisodeExists(episodeRecord, episodes))
                {
                    continue;
                }

                var existingSeason = existingSeasons.First(season => season.IndexNumber.HasValue && season.IndexNumber.Value == episodeRecord.AiredSeason);

                AddVirtualEpisode(episodeRecord, existingSeason);
            }
        }

        private Season AddVirtualSeason(int season, Series series)
        {
            string seasonName;
            if (season == 0)
            {
                seasonName = _libraryManager.GetLibraryOptions(series).SeasonZeroDisplayName;
            }
            else
            {
                seasonName = string.Format(
                    CultureInfo.InvariantCulture,
                    _localization.GetLocalizedString("NameSeasonNumber"),
                    season.ToString(CultureInfo.InvariantCulture));
            }

            _logger.LogInformation("Creating Season {SeasonName} entry for {SeriesName}", seasonName, series.Name);

            var newSeason = new Season
            {
                Name = seasonName,
                IndexNumber = season,
                Id = _libraryManager.GetNewItemId(
                    series.Id + season.ToString(CultureInfo.InvariantCulture) + seasonName,
                    typeof(Season)),
                IsVirtualItem = true,
                SeriesId = series.Id,
                SeriesName = series.Name,
                SeriesPresentationUniqueKey = series.GetPresentationUniqueKey()
            };

            series.AddChild(newSeason);

            return newSeason;
        }

        private void AddVirtualEpisode(EpisodeRecord? episode, Season? season)
        {
            // tvdb has a lot of bad data?
            if (!IsValidEpisode(episode) || season == null)
            {
                return;
            }

            // Put as much metadata into it as possible
            var newEpisode = new Episode
            {
                Name = episode!.EpisodeName,
                IndexNumber = episode.AiredEpisodeNumber!.Value,
                ParentIndexNumber = episode.AiredSeason!.Value,
                Id = _libraryManager.GetNewItemId(
                    season.Series.Id + episode.AiredSeason.Value.ToString(CultureInfo.InvariantCulture) + "Episode " + episode.AiredEpisodeNumber,
                    typeof(Episode)),
                IsVirtualItem = true,
                SeasonId = season.Id,
                SeriesId = season.Series.Id,
                AirsBeforeEpisodeNumber = episode.AirsBeforeEpisode,
                AirsAfterSeasonNumber = episode.AirsAfterSeason,
                AirsBeforeSeasonNumber = episode.AirsBeforeSeason,
                Overview = episode.Overview,
                CommunityRating = (float?)episode.SiteRating,
                OfficialRating = episode.ContentRating,
                SeriesName = season.Series.Name,
                SeriesPresentationUniqueKey = season.SeriesPresentationUniqueKey,
                SeasonName = season.Name,
                DateLastSaved = DateTime.UtcNow
            };
            if (DateTime.TryParse(episode!.FirstAired, out var premiereDate))
            {
                newEpisode.PremiereDate = premiereDate;
            }

            newEpisode.PresentationUniqueKey = newEpisode.GetPresentationUniqueKey();
            newEpisode.SetProviderId(MetadataProvider.Tvdb, episode.Id.ToString(CultureInfo.InvariantCulture));

            _logger.LogInformation(
                "Creating virtual episode {0} {1}x{2}",
                season.Series.Name,
                episode.AiredSeason,
                episode.AiredEpisodeNumber);

            season.AddChild(newEpisode);
        }
    }
}
