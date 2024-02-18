using System;
using System.Collections.Generic;
using System.Linq;

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
        /// Try to find a match for input language.
        /// </summary>
        /// <param name="language">input Language.</param>
        /// <param name="tvdbLanguage">TVDB data language.</param>
        /// <returns>Normalized language.</returns>
        public static bool MatchLanguage(string? language, string tvdbLanguage)
        {
            if (string.IsNullOrWhiteSpace(language))
            {
                return false;
            }

            // Unique case for zh-TW
            if (string.Equals(language, "zh-TW", StringComparison.OrdinalIgnoreCase))
            {
                language = "zhtw";
            }

            // Unique case for pt-BR
            if (string.Equals(language, "pt-br", StringComparison.OrdinalIgnoreCase))
            {
                language = "pt";
            }

            // try to find a match (ISO 639-2)
            return TvdbCultureInfo.GetCultureInfo(language)?.ThreeLetterISOLanguageNames?.Contains(tvdbLanguage, StringComparer.OrdinalIgnoreCase) ?? false;
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
