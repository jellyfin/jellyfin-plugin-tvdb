using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Schema;
using Jellyfin.Extensions;
using Jellyfin.Extensions.Json;
using MediaBrowser.Model.Globalization;

namespace Jellyfin.Plugin.Tvdb
{
    /// <summary>
    /// Tvdb culture info.
    /// </summary>
    public static class TvdbCultureInfo
    {
        private const string _cultureInfo = "Jellyfin.Plugin.Tvdb.iso6392.txt";
        private const string _countryInfo = "Jellyfin.Plugin.Tvdb.countries.json";
        private static readonly Assembly _assembly = typeof(TvdbCultureInfo).Assembly;
        private static List<CultureDto> _cultures = new List<CultureDto>();
        private static List<CountryInfo> _countries = new List<CountryInfo>();

        static TvdbCultureInfo()
        {
            LoadCultureInfo().Wait();
            LoadCountryInfo();
        }

        /// <summary>
        /// Loads culture info from embedded resource.
        /// </summary>
        private static async Task LoadCultureInfo()
        {
            List<CultureDto> cultureList = new List<CultureDto>();
            using var stream = _assembly.GetManifestResourceStream(_cultureInfo) ?? throw new InvalidOperationException($"Invalid resource path: '{_cultureInfo}'");
            using var reader = new StreamReader(stream);
            await foreach (var line in reader.ReadAllLinesAsync().ConfigureAwait(false))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var parts = line.Split('|');

                if (parts.Length == 5)
                {
                    string name = parts[3];
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    string twoCharName = parts[2];
                    if (string.IsNullOrWhiteSpace(twoCharName))
                    {
                        continue;
                    }

                    string[] threeletterNames;
                    if (string.IsNullOrWhiteSpace(parts[1]))
                    {
                        threeletterNames = new[] { parts[0] };
                    }
                    else
                    {
                        threeletterNames = new[] { parts[0], parts[1] };
                    }

                    cultureList.Add(new CultureDto(name, name, twoCharName, threeletterNames));
                }
            }

            _cultures = cultureList;
        }

        /// <summary>
        /// Loads country info from embedded resource.
        /// </summary>
        private static void LoadCountryInfo()
        {
            using var stream = _assembly.GetManifestResourceStream(_countryInfo) ?? throw new InvalidOperationException($"Invalid resource path: '{_countryInfo}'");
            using var reader = new StreamReader(stream);
            _countries = JsonSerializer.Deserialize<List<CountryInfo>>(reader.ReadToEnd(), JsonDefaults.Options) ?? throw new InvalidOperationException($"Resource contains invalid data: '{_countryInfo}'");
        }

        /// <summary>
        /// Gets the cultureinfo for the given language.
        /// </summary>
        /// <param name="language">Language.</param>
        /// <returns>CultureInfo.</returns>
        public static CultureDto? GetCultureInfo(string language)
        {
            for (var i = 0; i < _cultures.Count; i++)
            {
                var culture = _cultures[i];
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
            for (var i = 0; i < _countries.Count; i++)
            {
                var countryInfo = _countries[i];
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
