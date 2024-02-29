using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Dto;
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
    /// Tvdb person image provider.
    /// </summary>
    public class TvdbPersonImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TvdbPersonImageProvider> _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly TvdbClientManager _tvdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TvdbPersonImageProvider"/> class.
        /// </summary>
        /// <param name="libraryManager">Instance of the <see cref="ILibraryManager"/> interface.</param>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger{TvdbPersonImageProvider}"/> interface.</param>
        /// <param name="tvdbClientManager">Instance of <see cref="TvdbClientManager"/> interface.</param>
        public TvdbPersonImageProvider(
            ILibraryManager libraryManager,
            IHttpClientFactory httpClientFactory,
            ILogger<TvdbPersonImageProvider> logger,
            TvdbClientManager tvdbClientManager)
        {
            _libraryManager = libraryManager;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _tvdbClientManager = tvdbClientManager;
        }

        /// <inheritdoc />
        public string Name => TvdbPlugin.ProviderName;

        /// <inheritdoc />
        public bool Supports(BaseItem item) => item is Person;

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            yield return ImageType.Primary;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var seriesWithPerson = _libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Series },
                PersonIds = new[] { item.Id },
                DtoOptions = new DtoOptions(false)
                {
                    EnableImages = false
                }
            }).Cast<Series>()
                .Where(i => TvdbSeriesProvider.IsValidSeries(i.ProviderIds))
                .ToList();

            var infos = (await Task.WhenAll(seriesWithPerson.Select(async i =>
                        await GetImageFromSeriesData(i, item.Name, cancellationToken).ConfigureAwait(false)))
                    .ConfigureAwait(false))
                .Where(i => i != null)
                .Cast<RemoteImageInfo>()
                .Take(1);

            return infos;
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);
        }

        private async Task<RemoteImageInfo?> GetImageFromSeriesData(Series series, string personName, CancellationToken cancellationToken)
        {
            var tvdbId = Convert.ToInt32(series.GetProviderId(TvdbPlugin.ProviderId), CultureInfo.InvariantCulture);

            try
            {
                var actorsResult = await _tvdbClientManager
                    .GetSeriesExtendedByIdAsync(tvdbId, series.GetPreferredMetadataLanguage(), cancellationToken)
                    .ConfigureAwait(false);
                var character = actorsResult.Characters.FirstOrDefault(i => string.Equals(i.PersonName, personName, StringComparison.OrdinalIgnoreCase));

                if (character == null)
                {
                    return null;
                }

                var actor = await _tvdbClientManager
                    .GetActorAsync(character.PeopleId.GetValueOrDefault(), series.GetPreferredMetadataCountryCode(), cancellationToken)
                    .ConfigureAwait(false);
                return new RemoteImageInfo
                {
                    Url = actor.Image,
                    Type = ImageType.Primary,
                    ProviderName = Name
                };
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to retrieve actor {ActorName} from series {SeriesTvdbId}:{Name}", personName, tvdbId, series.Name);
                return null;
            }
        }
    }
}
