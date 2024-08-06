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
        private readonly TvdbClientManager _tvdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TvdbPersonImageProvider"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        /// <param name="logger">Instance of the <see cref="ILogger{TvdbPersonImageProvider}"/> interface.</param>
        /// <param name="tvdbClientManager">Instance of <see cref="TvdbClientManager"/> interface.</param>
        public TvdbPersonImageProvider(
            IHttpClientFactory httpClientFactory,
            ILogger<TvdbPersonImageProvider> logger,
            TvdbClientManager tvdbClientManager)
        {
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
            if (!item.TryGetProviderId(MetadataProvider.Tvdb, out var personTvdbId))
            {
                return Enumerable.Empty<RemoteImageInfo>();
            }

            var personTvdbIdInt = int.Parse(personTvdbId!, CultureInfo.InvariantCulture);
            try
            {
                var personResult = await _tvdbClientManager.GetActorExtendedAsync(personTvdbIdInt, cancellationToken).ConfigureAwait(false);
                if (personResult.Image is null)
                {
                    return Enumerable.Empty<RemoteImageInfo>();
                }

                return new List<RemoteImageInfo>
                {
                    new RemoteImageInfo
                    {
                        ProviderName = Name,
                        Type = ImageType.Primary,
                        Url = personResult.Image,
                    },
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve actor {ActorName} with {ActorTvdbId}", item.Name, personTvdbId);
                return Enumerable.Empty<RemoteImageInfo>();
            }
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);
        }
    }
}
