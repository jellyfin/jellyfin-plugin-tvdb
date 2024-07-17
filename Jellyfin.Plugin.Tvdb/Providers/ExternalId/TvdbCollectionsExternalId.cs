using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Tvdb.Providers.ExternalId
{
    /// <inheritdoc/>
    public class TvdbCollectionsExternalId : IExternalId
    {
        /// <inheritdoc/>
        public string ProviderName => TvdbPlugin.ProviderName;

        /// <inheritdoc/>
        public string Key => TvdbPlugin.CollectionProviderId;

        /// <inheritdoc/>
        public ExternalIdMediaType? Type => ExternalIdMediaType.BoxSet;

        /// <inheritdoc/>
        public string? UrlFormatString => null;

        /// <inheritdoc/>
        public bool Supports(IHasProviderIds item)
        {
            return item is Movie || item is Series;
        }
    }
}
