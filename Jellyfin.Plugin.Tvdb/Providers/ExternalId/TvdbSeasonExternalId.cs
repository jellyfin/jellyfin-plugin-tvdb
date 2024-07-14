using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Tvdb.Providers.ExternalId
{
    /// <inheritdoc />
    public class TvdbSeasonExternalId : IExternalId
    {
        /// <inheritdoc />
        public string ProviderName => TvdbPlugin.ProviderName;

        /// <inheritdoc />
        public string Key => TvdbPlugin.ProviderId;

        /// <inheritdoc />
        public ExternalIdMediaType? Type => ExternalIdMediaType.Season;

        /// <inheritdoc />
        public string UrlFormatString => TvdbUtils.TvdbBaseUrl + "dereferrer/season/{0}";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item) => item is Season;
    }
}
