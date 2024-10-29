using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Tvdb.Providers.ExternalId
{
    /// <inheritdoc />
    public class TvdbEpisodeExternalId : IExternalId
    {
        /// <inheritdoc />
        public string ProviderName => TvdbPlugin.ProviderName;

        /// <inheritdoc />
        public string Key => TvdbPlugin.ProviderId;

        /// <inheritdoc />
        public ExternalIdMediaType? Type => ExternalIdMediaType.Episode;

        /// <inheritdoc />
        public string? UrlFormatString => null;

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item) => item is Episode;
    }
}
