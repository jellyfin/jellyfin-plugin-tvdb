using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Extensions;
using MediaBrowser.Model.Globalization;

namespace Jellyfin.Plugin.Tvdb
{
    /// <summary>
    /// Tvdb culture info.
    /// </summary>
    public class TvdbCultureInfo
    {
        private static IEnumerable<CultureDto> _cultures = new List<CultureDto>();
        private static IEnumerable<CountryInfo> _countries = new List<CountryInfo>();
        private readonly ILocalizationManager _localizationManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TvdbCultureInfo"/> class.
        /// </summary>
        /// <param name="localizationManager">Instance of the <see cref="ILocalizationManager"/> interface.</param>
        public TvdbCultureInfo(ILocalizationManager localizationManager)
        {
            _localizationManager = localizationManager;
            _countries = _localizationManager.GetCountries();
            _cultures = _localizationManager.GetCultures();
        }

        /// <summary>
        /// Gets the cultureinfo for the given language.
        /// </summary>
        /// <param name="language">Language.</param>
        /// <returns>CultureInfo.</returns>
        public static CultureDto? GetCultureInfo(string language)
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
        public static CountryInfo? GetCountryInfo(string country)
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
