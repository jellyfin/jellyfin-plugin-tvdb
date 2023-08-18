using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Schema;
using Jellyfin.Extensions;
using MediaBrowser.Model.Globalization;

namespace Jellyfin.Plugin.Tvdb
{
    /// <summary>
    /// Tvdb culture info.
    /// </summary>
    public static class TvdbCultureInfo
    {
        private const string _resourceName = "Jellyfin.Plugin.Tvdb.iso6392.txt";
        private static readonly Assembly _assembly = typeof(TvdbCultureInfo).Assembly;
        private static List<CultureDto> _cultures = new List<CultureDto>();

        static TvdbCultureInfo()
        {
            LoadCultureInfo().Wait();
        }

        /// <summary>
        /// Loads culture info from embedded resource.
        /// </summary>
        private static async Task LoadCultureInfo()
        {
            List<CultureDto> cultureList = new List<CultureDto>();
            using var stream = _assembly.GetManifestResourceStream(_resourceName) ?? throw new InvalidOperationException($"Invalid resource path: '{_resourceName}'");
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
    }
}
