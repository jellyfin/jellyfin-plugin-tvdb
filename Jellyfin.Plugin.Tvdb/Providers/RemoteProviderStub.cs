using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;

namespace Jellyfin.Plugin.Tvdb.Providers
{
    /// <summary>
    /// The metadata provider class for allowing Library-based configuration.
    /// </summary>
    public class RemoteProviderStub : IMetadataProvider<Series>, IRemoteMetadataProvider
    {
        /// <inheritdoc />
        public string Name => TvdbMissingEpisodeProvider.ProviderName;
    }
}
