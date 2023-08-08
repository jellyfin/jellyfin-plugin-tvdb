using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Extensions;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using TvDbSharper;

namespace Jellyfin.Plugin.Tvdb.Providers
{
    /// <summary>
    /// TvdbEpisodeProvider.
    /// </summary>
    public class TvdbEpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TvdbEpisodeProvider> _logger;
        private readonly TvdbClientManager _tvdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TvdbEpisodeProvider"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger{TvdbEpisodeProvider}"/> interface.</param>
        /// <param name="tvdbClientManager">Instance of <see cref="TvdbClientManager"/>.</param>
        public TvdbEpisodeProvider(IHttpClientFactory httpClientFactory, ILogger<TvdbEpisodeProvider> logger, TvdbClientManager tvdbClientManager)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _tvdbClientManager = tvdbClientManager;
        }

        /// <inheritdoc />
        public string Name => TvdbPlugin.ProviderName;

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        {
            var list = new List<RemoteSearchResult>();

            // Either an episode number or date must be provided; and the dictionary of provider ids must be valid
            if ((searchInfo.IndexNumber == null && searchInfo.PremiereDate == null)
                || !TvdbSeriesProvider.IsValidSeries(searchInfo.SeriesProviderIds))
            {
                return list;
            }

            var metadataResult = await GetEpisode(searchInfo, cancellationToken).ConfigureAwait(false);

            if (!metadataResult.HasMetadata)
            {
                return list;
            }

            var item = metadataResult.Item;

            list.Add(new RemoteSearchResult
            {
                IndexNumber = item.IndexNumber,
                Name = item.Name,
                ParentIndexNumber = item.ParentIndexNumber,
                PremiereDate = item.PremiereDate,
                ProductionYear = item.ProductionYear,
                ProviderIds = item.ProviderIds,
                SearchProviderName = Name,
                IndexNumberEnd = item.IndexNumberEnd
            });

            return list;
        }

        /// <inheritdoc />
        public async Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Episode>
            {
                QueriedById = true
            };

            if (TvdbSeriesProvider.IsValidSeries(info.SeriesProviderIds) &&
                (info.IndexNumber.HasValue || info.PremiereDate.HasValue))
            {
                // Check for multiple episodes per file, if not run one query.
                if (info.IndexNumberEnd.HasValue)
                {
                    _logger.LogDebug("Multiple episodes found in {Path}", info.Path);

                    result = await GetCombinedEpisode(info, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    result = await GetEpisode(info, cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                _logger.LogDebug("No series identity found for {EpisodeName}", info.Name);
            }

            return result;
        }

        private async Task<MetadataResult<Episode>> GetCombinedEpisode(EpisodeInfo info, CancellationToken cancellationToken)
        {
            var startIndex = info.IndexNumber;
            var endIndex = info.IndexNumberEnd;

            List<MetadataResult<Episode>> results = new List<MetadataResult<Episode>>();

            for (int? episode = startIndex; episode <= endIndex; episode++)
            {
                var tempEpisodeInfo = info;
                info.IndexNumber = episode;

                results.Add(await GetEpisode(tempEpisodeInfo, cancellationToken).ConfigureAwait(false));
            }

            var result = CombineResults(results);

            return result;
        }

        private MetadataResult<Episode> CombineResults(List<MetadataResult<Episode>> results)
        {
            // Use first result as baseline
            var result = results[0];

            var name = new StringBuilder(result.Item.Name);
            var overview = new StringBuilder(result.Item.Overview);

            for (int res = 1; res < results.Count; res++)
            {
                name.Append(" / ");
                name.Append(results[res].Item.Name);
                overview.Append(" / ");
                overview.Append(results[res].Item.Overview);
            }

            result.Item.Name = name.ToString();
            result.Item.Overview = overview.ToString();

            return result;
        }

        private async Task<MetadataResult<Episode>> GetEpisode(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Episode>
            {
                QueriedById = true
            };

            var seriesTvdbId = searchInfo.SeriesProviderIds.FirstOrDefault(x => x.Key == TvdbPlugin.ProviderId).Value;
            string? episodeTvdbId = null;
            try
            {
                episodeTvdbId = await _tvdbClientManager
                    .GetEpisodeTvdbId(searchInfo, searchInfo.MetadataLanguage, cancellationToken)
                    .ConfigureAwait(false);
                if (string.IsNullOrEmpty(episodeTvdbId))
                {
                    _logger.LogError(
                        "Episode {SeasonNumber}x{EpisodeNumber} not found for series {SeriesTvdbId}:{Name}",
                        searchInfo.ParentIndexNumber,
                        searchInfo.IndexNumber,
                        seriesTvdbId,
                        searchInfo.Name);
                    return result;
                }

                var episodeResult = await _tvdbClientManager.GetEpisodesAsync(
                    Convert.ToInt32(episodeTvdbId, CultureInfo.InvariantCulture),
                    searchInfo.MetadataLanguage,
                    cancellationToken).ConfigureAwait(false);

                result = MapEpisodeToResult(searchInfo, episodeResult.Data);
            }
            catch (TvDbServerException e)
            {
                _logger.LogError(
                    e,
                    "Failed to retrieve episode with id {EpisodeTvDbId}, series id {SeriesTvdbId}:{Name}",
                    episodeTvdbId,
                    seriesTvdbId,
                    searchInfo.Name);
            }

            return result;
        }

        private static MetadataResult<Episode> MapEpisodeToResult(EpisodeInfo id, EpisodeExtendedRecordDto episode)
        {
            var result = new MetadataResult<Episode>
            {
                HasMetadata = true,
                Item = new Episode
                {
                    IndexNumber = id.IndexNumber,
                    ParentIndexNumber = id.ParentIndexNumber,
                    IndexNumberEnd = id.IndexNumberEnd,
                    AirsBeforeEpisodeNumber = episode.AirsBeforeEpisode,
                    AirsAfterSeasonNumber = episode.AirsAfterSeason,
                    AirsBeforeSeasonNumber = episode.AirsBeforeSeason,
                    // Tvdb uses 3 letter code for language
                    Name = episode.Translations.NameTranslations.FirstOrDefault(x => x.Language.Substring(0, 2) == id.MetadataLanguage)?.Name,
                    Overview = episode.Translations.OverviewTranslations.FirstOrDefault(x => x.Language.Substring(0, 2) == id.MetadataLanguage)?.Overview,
                }
            };
            result.ResetPeople();

            var item = result.Item;
            item.SetProviderId(TvdbPlugin.ProviderId, episode.Id.ToString(CultureInfo.InvariantCulture));
            var imdbID = episode.RemoteIds.FirstOrDefault(x => x.SourceName == "IMDB")?.Id;
            if (!string.IsNullOrEmpty(imdbID))
            {
                item.SetProviderId(MetadataProvider.Imdb, imdbID);
            }

            if (string.Equals(id.SeriesDisplayOrder, "dvd", StringComparison.OrdinalIgnoreCase))
            {
                var dvdInfo = episode.Seasons.FirstOrDefault(x => x.Type.Name == "dvd");
                if (dvdInfo is null)
                {
                    item.IndexNumber = episode.Number;
                }
                else
                {
                    item.IndexNumber = Convert.ToInt32(dvdInfo.Number, CultureInfo.InvariantCulture);
                }

                item.ParentIndexNumber = episode.SeasonNumber;
            }
            else if (string.Equals(id.SeriesDisplayOrder, "absolute", StringComparison.OrdinalIgnoreCase))
            {
                var absoluteInfo = episode.Seasons.FirstOrDefault(x => x.Type.Name == "absolute");
                if (absoluteInfo is not null)
                {
                    item.IndexNumber = Convert.ToInt32(absoluteInfo.Number, CultureInfo.InvariantCulture);
                }
            }
            else
            {
                item.IndexNumber = episode.Number;
                item.ParentIndexNumber = episode.SeasonNumber;
            }

            if (DateTime.TryParse(episode.Aired, out var date))
            {
                // dates from tvdb are UTC but without offset or Z
                item.PremiereDate = date;
                item.ProductionYear = date.Year;
            }

            if (episode.Characters is not null)
            {
                for (var i = 0; i < episode.Characters.Length; ++i)
                {
                    var currentActor = episode.Characters[i];
                    if (currentActor.PeopleType == "Actor")
                    {
                        result.AddPerson(new PersonInfo
                        {
                            Type = PersonType.Actor,
                            Name = currentActor.PersonName,
                            Role = currentActor.Name
                        });
                    }
                    else if (currentActor.PeopleType == "Director")
                    {
                        result.AddPerson(new PersonInfo
                        {
                            Type = PersonType.Director,
                            Name = currentActor.PersonName
                        });
                    }
                    else if (currentActor.PeopleType == "Writer")
                    {
                        result.AddPerson(new PersonInfo
                        {
                            Type = PersonType.Writer,
                            Name = currentActor.PersonName
                        });
                    }
                    else if (currentActor.PeopleType == "Guest Star")
                    {
                        result.AddPerson(new PersonInfo
                        {
                            Type = PersonType.GuestStar,
                            Name = currentActor.PersonName,
                            Role = currentActor.Name
                        });
                    }
                }
            }

            // result.ResultLanguage = episode.Language.EpisodeName;
            return result;
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);
        }
    }
}
