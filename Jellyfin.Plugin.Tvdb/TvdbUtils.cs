using System;
using System.Globalization;
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
        public const string BannerUrl = TvdbBaseUrl + "banners/";

        /// <summary>
        /// Get image type from key type.
        /// </summary>
        /// <param name="keyType">Key type.</param>
        /// <returns>Image type.</returns>
        /// <exception cref="ArgumentException">Unknown key type.</exception>
        public static ImageType GetImageTypeFromKeyType(string? keyType)
        {
            if (!string.IsNullOrEmpty(keyType))
            {
                switch (keyType.ToLowerInvariant())
                {
                    case "poster": return ImageType.Primary;
                    case "banner": return ImageType.Banner;
                    case "background": return ImageType.Backdrop;
                    case "clearlogo": return ImageType.Logo;
                    default: throw new ArgumentException($"Invalid or unknown keytype: {keyType}", nameof(keyType));
                }
            }

            throw new ArgumentException($"Null keytype");
        }

        /// <summary>
        /// Normalize language to tvdb format.
        /// </summary>
        /// <param name="language">Language.</param>
        /// <returns>Normalized language.</returns>
        public static string? NormalizeLanguageToTvdb(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return null;
            }

            // Unique case for zh-TW
            if (string.Equals(language, "zh-TW", StringComparison.OrdinalIgnoreCase))
            {
                return "zhtw";
            }

            // to (ISO 639-2)
            return TvdbCultureInfo.GetCultureInfo(language.Split('-')[0].ToLowerInvariant())?.ThreeLetterISOLanguageName;
        }

        /// <summary>
        /// Normalize language to jellyfin format.
        /// </summary>
        /// <param name="language">Language.</param>
        /// <returns>Normalized language.</returns>
        public static string? NormalizeLanguageToJellyfin(string? language)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return null;
            }

            // Unique case for zhtw
            if (string.Equals(language, "zhtw", StringComparison.OrdinalIgnoreCase))
            {
                return "zh-TW";
            }

            // to (ISO 639-1)
            return TvdbCultureInfo.GetCultureInfo(language)?.TwoLetterISOLanguageName;
        }
    }
}
