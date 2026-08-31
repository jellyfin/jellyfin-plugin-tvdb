using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        /// Checks whether an image url returned by the api points to an actual image.
        /// </summary>
        /// <remarks>
        /// The api returns its image base url, e.g. <c>https://www.thetvdb.com/banners/</c>, for records without
        /// an image. Those urls address a directory instead of a file and can never be downloaded.
        /// </remarks>
        /// <param name="url">The url to check.</param>
        /// <returns><c>true</c> if the url points to an image, <c>false</c> otherwise.</returns>
        public static bool IsValidImageUrl([NotNullWhen(true)] string? url)
        {
            if (string.IsNullOrWhiteSpace(url)
                || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
                || (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    && !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            var path = uri.AbsolutePath;
            return path.LastIndexOf('/') < path.Length - 1;
        }

        /// <summary>
        /// Returns the url if it points to an actual image, <see langword="null"/> otherwise.
        /// </summary>
        /// <param name="url">The url to check.</param>
        /// <returns>The image url, or <see langword="null"/>.</returns>
        public static string? GetImageUrlOrDefault(string? url)
        {
            return IsValidImageUrl(url) ? url : null;
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
