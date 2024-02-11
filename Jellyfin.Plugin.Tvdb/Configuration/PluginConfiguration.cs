using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Tvdb.Configuration
{
    /// <summary>
    /// Configuration for tvdb.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Gets the tvdb api key for project.
        /// </summary>
        public const string ProjectApiKey = "";

        /// <summary>
        /// Gets or sets the tvdb api key for user.
        /// </summary>
        /// <remarks>
        /// This is the subscriber's pin.
        /// </remarks>
        public string ApiKey { get; set; } = string.Empty;
    }
}
