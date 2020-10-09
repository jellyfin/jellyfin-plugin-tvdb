using System;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Tvdb
{
    /// <summary>
    /// Tvdb utils.
    /// </summary>
    public static class TvdbUtils
    {
        /// <summary>
        /// Base url for all requests.
        /// </summary>
        public const string TvdbBaseUrl = "https://www.thetvdb.com/";

        /// <summary>
        /// Base url for banners.
        /// </summary>
        public const string BannerUrl = TvdbBaseUrl + "/banners/";

        /// <summary>
        /// Get image type from key type.
        /// </summary>
        /// <param name="keyType">Key type.</param>
        /// <returns>Image type.</returns>
        /// <exception cref="ArgumentException">Unknown key type.</exception>
        public static ImageType GetImageTypeFromKeyType(string keyType)
        {
            switch (keyType.ToLowerInvariant())
            {
                case "poster":
                case "season": return ImageType.Primary;
                case "series":
                case "seasonwide": return ImageType.Banner;
                case "fanart": return ImageType.Backdrop;
                default: throw new ArgumentException($"Invalid or unknown keytype: {keyType}", nameof(keyType));
            }
        }

        /// <summary>
        /// Normalize language to tvdb format.
        /// </summary>
        /// <param name="language">Language.</param>
        /// <returns>Normalized language.</returns>
        public static string? NormalizeLanguage(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return null;
            }

            // pt-br is just pt to tvdb
            return language.Split('-')[0].ToLowerInvariant();
        }
    }
}