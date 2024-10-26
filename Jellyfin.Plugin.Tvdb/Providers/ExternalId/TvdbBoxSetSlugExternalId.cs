using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Tvdb.Providers.ExternalId
{
    /// <summary>
    /// The TvdbBoxSetSlugExternalId class.
    /// </summary>
    public class TvdbBoxSetSlugExternalId : IExternalId
    {
        /// <inheritdoc />
        public string ProviderName => TvdbPlugin.ProviderName;

        /// <inheritdoc />
        public string Key => TvdbPlugin.SlugProviderId;

        /// <inheritdoc />
        public ExternalIdMediaType? Type => ExternalIdMediaType.BoxSet;

        /// <inheritdoc />
        public string? UrlFormatString => TvdbUtils.TvdbBaseUrl + "lists/{0}";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item) => item is BoxSet;
    }
}
