using System;
using System.Linq;
using Jellyfin.Extensions;
using MediaBrowser.Model.Globalization;

namespace Jellyfin.Plugin.Tvdb
{
    /// <summary>
    /// Tvdb culture info.
    /// </summary>
    internal static class TvdbCultureInfo
    {
        private static CultureDto[] _cultures = Array.Empty<CultureDto>();
        private static CountryInfo[] _countries = Array.Empty<CountryInfo>();

        internal static void SetCultures(CultureDto[] cultures)
        {
            _cultures = cultures;
        }

        internal static void SetCountries(CountryInfo[] countries)
        {
            _countries = countries;
        }

        /// <summary>
        /// Gets the cultureinfo for the given language.
        /// </summary>
        /// <param name="language">Language.</param>
        /// <returns>CultureInfo.</returns>
        internal static CultureDto? GetCultureInfo(string language)
        {
            foreach (var culture in _cultures)
            {
                if (language.Equals(culture.DisplayName, StringComparison.OrdinalIgnoreCase)
                    || language.Equals(culture.Name, StringComparison.OrdinalIgnoreCase)
                    || culture.ThreeLetterISOLanguageNames.Contains(language, StringComparison.OrdinalIgnoreCase)
                    || language.Equals(culture.TwoLetterISOLanguageName, StringComparison.OrdinalIgnoreCase))
                {
                    return culture;
                }
            }

            return default;
        }

        /// <summary>
        /// Gets the CountryInfo for the given country.
        /// </summary>
        /// <param name="country"> Country.</param>
        /// <returns>CountryInfo.</returns>
        internal static CountryInfo? GetCountryInfo(string country)
        {
            foreach (var countryInfo in _countries)
            {
                if (country.Equals(countryInfo.Name, StringComparison.OrdinalIgnoreCase)
                    || country.Equals(countryInfo.TwoLetterISORegionName, StringComparison.OrdinalIgnoreCase)
                    || country.Equals(countryInfo.ThreeLetterISORegionName, StringComparison.OrdinalIgnoreCase))
                {
                    return countryInfo;
                }
            }

            return default;
        }
    }
}
