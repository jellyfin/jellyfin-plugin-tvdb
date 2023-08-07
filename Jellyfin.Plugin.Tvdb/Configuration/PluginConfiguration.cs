using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Tvdb.Configuration
{
    /// <summary>
    /// Configuration for tvdb.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Gets or sets the tvdb api key for project.
        /// </summary>
        public string ProjectApiKey { get; set; } = "452b27c4-f752-4df5-b41c-5d55ecb2877e";

        /// <summary>
        /// Gets or sets the tvdb api key for user.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;
    }
}
