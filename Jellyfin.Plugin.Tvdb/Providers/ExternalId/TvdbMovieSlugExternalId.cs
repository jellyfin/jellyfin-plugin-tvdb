using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Tvdb.Providers.ExternalId
{
    /// <inheritdoc />
    public class TvdbMovieSlugExternalId : IExternalId
    {
        /// <inheritdoc />
        public string ProviderName => TvdbPlugin.ProviderName;

        /// <inheritdoc />
        public string Key => TvdbPlugin.SlugProviderId;

        /// <inheritdoc />
        public ExternalIdMediaType? Type => ExternalIdMediaType.Movie;

        /// <inheritdoc />
        public string? UrlFormatString => null;

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item) => item is Movie;
    }
}
