using System;
using System.Collections.Generic;
using MediaBrowser.Model.Entities;
using Tvdb.Sdk;

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

            throw new ArgumentException("Null keytype");
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

            // Unique case for pt-BR
            if (string.Equals(language, "pt-br", StringComparison.OrdinalIgnoreCase))
            {
                return "pt";
            }

            // to (ISO 639-2)
            return TvdbCultureInfo.GetCultureInfo(language)?.ThreeLetterISOLanguageName;
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

            // Unique case for pt
            if (string.Equals(language, "pt", StringComparison.OrdinalIgnoreCase))
            {
                return "pt-BR";
            }

            // to (ISO 639-1)
            return TvdbCultureInfo.GetCultureInfo(language)?.TwoLetterISOLanguageName;
        }

        /// <summary>
        /// Converts SeriesAirsDays to DayOfWeek array.
        /// </summary>
        /// <param name="seriesAirsDays">SeriesAirDays.</param>
        /// <returns>List{DayOfWeek}.</returns>
        public static IEnumerable<DayOfWeek> GetAirDays(SeriesAirsDays seriesAirsDays)
        {
            if (seriesAirsDays.Sunday)
            {
                yield return DayOfWeek.Sunday;
            }

            if (seriesAirsDays.Monday)
            {
                yield return DayOfWeek.Monday;
            }

            if (seriesAirsDays.Tuesday)
            {
                yield return DayOfWeek.Tuesday;
            }

            if (seriesAirsDays.Wednesday)
            {
                yield return DayOfWeek.Wednesday;
            }

            if (seriesAirsDays.Thursday)
            {
                yield return DayOfWeek.Thursday;
            }

            if (seriesAirsDays.Friday)
            {
                yield return DayOfWeek.Friday;
            }

            if (seriesAirsDays.Saturday)
            {
                yield return DayOfWeek.Saturday;
            }
        }
    }
}
