using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Tvdb.Configuration
{
    /// <summary>
    /// Configuration for tvdb.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Gets or sets the tvdb api key.
        /// </summary>
        public string ApiKey { get; set; } = "e2dbbd55-0a17-456d-b7ce-fa72c5fbee90";
    }
}
