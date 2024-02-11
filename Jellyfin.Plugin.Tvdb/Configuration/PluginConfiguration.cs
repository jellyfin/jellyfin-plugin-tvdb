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
        public const string ProjectApiKey = "7f7eed88-2530-4f84-8ee7-f154471b8f87";

        /// <summary>
        /// Gets or sets the tvdb api key for user.
        /// </summary>
        /// <remarks>
        /// This is the subscriber's pin.
        /// </remarks>
        public string ApiKey { get; set; } = string.Empty;
    }
}
