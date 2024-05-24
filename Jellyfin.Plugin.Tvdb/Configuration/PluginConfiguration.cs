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
        private int _cacheDurationInHours = 1;
        private int _cacheDurationInDays = 7;
        private int _metadataUpdateInHours = 2;

        /// <summary>
        /// Gets or sets the tvdb api key for user.
        /// </summary>
        /// <remarks>
        /// This is the subscriber's pin.
        /// </remarks>
        public string SubscriberPIN { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the cache in hours.
        /// </summary>
        public int CacheDurationInHours
        {
            get => _cacheDurationInHours;
            set => _cacheDurationInHours = value < 1 ? 1 : value;
        }

        /// <summary>
        /// Gets or sets the cache in days.
        /// </summary>
        public int CacheDurationInDays
        {
            get => _cacheDurationInDays;
            set => _cacheDurationInDays = value < 1 ? 7 : value;
        }

        /// <summary>
        /// Gets or sets the metadata update in hours.
        /// </summary>
        public int MetadataUpdateInHours
        {
            get => _metadataUpdateInHours;
            set => _metadataUpdateInHours = value < 1 ? 1 : value;
        }

        /// <summary>
        /// Gets or sets the fallback languages.
        /// </summary>
        public string FallbackLanguages { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether to import season name.
        /// </summary>
        public bool ImportSeasonName { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether to include missing specials.
        /// </summary>
        public bool IncludeMissingSpecials { get; set; } = true;
    }
}
