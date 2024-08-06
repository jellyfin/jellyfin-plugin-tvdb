using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Tvdb.Providers.ExternalId
{
    /// <summary>
    /// TvdbPersonExternalId.
    /// </summary>
    public class TvdbPersonExternalId : IExternalId
    {
        /// <inheritdoc />
        public string ProviderName => TvdbPlugin.ProviderName;

        /// <inheritdoc />
        public string Key => TvdbPlugin.ProviderId;

        /// <inheritdoc />
        public ExternalIdMediaType? Type => ExternalIdMediaType.Person;

        /// <inheritdoc />
        public string? UrlFormatString => TvdbUtils.TvdbBaseUrl + "people/{0}";

        /// <inheritdoc />
        public bool Supports(IHasProviderIds item) => item is Person;
    }
}
