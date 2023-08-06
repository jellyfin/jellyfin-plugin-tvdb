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
        public string ProjectApiKey { get; set; } = "dummyapi here";

        /// <summary>
        /// Gets or sets the tvdb api key for user.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;
    }
}
