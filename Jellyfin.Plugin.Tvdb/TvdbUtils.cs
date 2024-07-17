using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
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

        private static bool FallbackToOriginalLanguage => TvdbPlugin.Instance?.Configuration.FallbackToOriginalLanguage ?? false;

        /// <summary>
        /// Converts SeriesAirsDays to DayOfWeek array.
        /// </summary>
        /// <param name="seriesAirsDays">SeriesAirDays.</param>
        /// <returns>List{DayOfWeek}.</returns>
        public static IEnumerable<DayOfWeek> GetAirDays(SeriesAirsDays seriesAirsDays)
        {
            if (seriesAirsDays.Sunday.GetValueOrDefault())
            {
                yield return DayOfWeek.Sunday;
            }

            if (seriesAirsDays.Monday.GetValueOrDefault())
            {
                yield return DayOfWeek.Monday;
            }

            if (seriesAirsDays.Tuesday.GetValueOrDefault())
            {
                yield return DayOfWeek.Tuesday;
            }

            if (seriesAirsDays.Wednesday.GetValueOrDefault())
            {
                yield return DayOfWeek.Wednesday;
            }

            if (seriesAirsDays.Thursday.GetValueOrDefault())
            {
                yield return DayOfWeek.Thursday;
            }

            if (seriesAirsDays.Friday.GetValueOrDefault())
            {
                yield return DayOfWeek.Friday;
            }

            if (seriesAirsDays.Saturday.GetValueOrDefault())
            {
                yield return DayOfWeek.Saturday;
            }
        }

        /// <summary>
        /// Returns the original language if fallback is enabled.
        /// </summary>
        /// <param name="text">String to return if fallback is enabled.</param>
        /// <returns>string or null.</returns>
        public static string? ReturnOriginalLanguageOrDefault(string? text)
        {
            return FallbackToOriginalLanguage ? text : null;
        }

        /// <summary>
        /// Gets the name of the comparable.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <returns>System.String.</returns>
        public static string GetComparableName(string name)
        {
            name = name.ToLowerInvariant();
            name = name.Normalize(NormalizationForm.FormC);
            name = name.Replace(", the", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("the ", " ", StringComparison.OrdinalIgnoreCase)
                .Replace(" the ", " ", StringComparison.OrdinalIgnoreCase);
            name = name.Replace("&", " and ", StringComparison.OrdinalIgnoreCase);
            name = Regex.Replace(name, @"[\p{Lm}\p{Mn}]", string.Empty); // Remove diacritics, etc
            name = Regex.Replace(name, @"[\W\p{Pc}]+", " "); // Replace sequences of non-word characters and _ with " "
            return name.Trim();
        }
    }
}
