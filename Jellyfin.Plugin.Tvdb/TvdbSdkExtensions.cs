using System.Linq;

using Jellyfin.Extensions;

using Tvdb.Sdk;

namespace Jellyfin.Plugin.Tvdb;

/// <summary>
/// Extension Methods for Tvdb SDK.
/// </summary>
public static class TvdbSdkExtensions
{
    /// <summary>
    /// Get the translated Name, or <see langword="null"/>.
    /// </summary>
    /// <param name="translations">Available translations.</param>
    /// <param name="language">Requested language.</param>
    /// <returns>Translated Name, or <see langword="null"/>.</returns>
    public static string? GetTranslatedNamedOrDefault(this TranslationExtended? translations, string? language)
    {
        return translations?
            .NameTranslations?
            .FirstOrDefault(translation => TvdbUtils.MatchLanguage(language, translation.Language))?
            .Name;
    }

    /// <summary>
    /// Get the translated Overview, or <see langword="null"/>.
    /// </summary>
    /// <param name="translations">Available translations.</param>
    /// <param name="language">Requested language.</param>
    /// <returns>Translated Overview, or <see langword="null"/>.</returns>
    public static string? GetTranslatedOverviewOrDefault(this TranslationExtended? translations, string? language)
    {
        return translations?
            .OverviewTranslations?
            .FirstOrDefault(translation => TvdbUtils.MatchLanguage(language, translation.Language))?
            .Overview;
    }
}
