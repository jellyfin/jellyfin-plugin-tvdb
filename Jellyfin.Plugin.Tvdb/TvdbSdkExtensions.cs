using System;
using System.Globalization;
using System.Linq;

using Jellyfin.Extensions;

using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

using Tvdb.Sdk;

namespace Jellyfin.Plugin.Tvdb;

/// <summary>
/// Extension Methods for Tvdb SDK.
/// </summary>
public static class TvdbSdkExtensions
{
    /// <summary>
    /// Gets the fallback languages which have been selected, ignoring whitespace and empty entries.
    /// </summary>
    private static string[]? FallbackLanguages => TvdbPlugin.Instance?.Configuration.FallbackLanguages.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

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
            .FirstOrDefault(translation => IsMatch(translation.Language, language) && translation.IsAlias != true)?
            .Name
            ?? FallbackLanguages?
            .Select(lang => translations?
                .NameTranslations?
                .FirstOrDefault(translation => IsMatch(translation.Language, lang) && translation.IsAlias != true)?
                .Name)
            .FirstOrDefault(name => name != null);
    }

    /// <summary>
    /// Get the translated Name, or <see langword="null"/>.
    /// </summary>
    /// <param name="translations">Available translations.</param>
    /// <param name="language">Requested language.</param>
    /// <returns>Translated Name, or <see langword="null"/>.</returns>
    public static string? GetTranslatedNamedOrDefaultIgnoreAlias(this TranslationExtended? translations, string? language)
    {
        return translations?
            .NameTranslations?
            .FirstOrDefault(translation => IsMatch(translation.Language, language))?
            .Name
            ?? FallbackLanguages?
            .Select(lang => translations?
                .NameTranslations?
                .FirstOrDefault(translation => IsMatch(translation.Language, lang))?
                .Name)
            .FirstOrDefault(name => name != null);
    }

    /// <summary>
    /// Get the translated Name, or <see langword="null"/>.
    /// </summary>
    /// <param name="translations">Available translations.</param>
    /// <param name="language">Requested language.</param>
    /// <returns>Translated Name, or <see langword="null"/>.</returns>
    public static string? GetTranslatedNamedOrDefault(this TranslationSimple? translations, string? language)
    {
        return translations?
            .FirstOrDefault(translation => IsMatch(translation.Key, language))
            .Value
            ?? FallbackLanguages?
            .Select(lang => translations?
                .FirstOrDefault(translation => IsMatch(translation.Key, lang))
                .Value)
            .FirstOrDefault(name => name != null);
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
            .FirstOrDefault(translation => IsMatch(translation.Language, language))?
            .Overview
            ?? FallbackLanguages?
            .Select(lang => translations?
                .OverviewTranslations?
                .FirstOrDefault(translation => IsMatch(translation.Language, lang))?
            .Overview)
            .FirstOrDefault(overview => overview != null);
    }

    private static bool IsMatch(this string translation, string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return false;
        }

        language = language?.ToLowerInvariant() switch
        {
            "zh-tw" => "zhtw", // Unique case for zh-TW
            "pt-br" => "pt", // Unique case for pt-BR0
            "pt-pt" => "por", // Unique case for pt-PT
            _ => language,
        };

        if (translation.Equals(language, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // try to find a match (ISO 639-2)
        return TvdbCultureInfo.GetCultureInfo(language!)?
            .ThreeLetterISOLanguageNames?
            .Contains(translation, StringComparer.OrdinalIgnoreCase)
            ?? false;
    }

    /// <summary>
    /// Normalize <see cref="Language"/> to jellyfin format.
    /// </summary>
    /// <remarks>TVDb uses 3 character language.</remarks>
    /// <param name="language">The <see cref="Language"/>.</param>
    /// <returns>Normalized language.</returns>
    private static string? NormalizeToJellyfin(this Language? language)
    {
        return language?.Id?.ToLowerInvariant() switch
        {
            "zhtw" => "zh-TW", // Unique case for zhtw
            "pt" => "pt-BR", // Unique case for pt
            "por" => "pt-PT", // Unique case for por
            var tvdbLang when tvdbLang is { } => TvdbCultureInfo.GetCultureInfo(tvdbLang)?.TwoLetterISOLanguageName, // to (ISO 639-1)
            _ => null,
        };
    }

    /// <summary>
    /// Get <see cref="ImageType"/> from <see cref="ArtworkType"/>.
    /// </summary>
    /// <param name="artworkType">A <see cref="ArtworkType"/>.</param>
    /// <returns><see cref="ImageType"/> or <see langword="null"/> if type is unknown.</returns>0
    public static ImageType? GetImageType(this ArtworkType? artworkType)
    {
        return artworkType?.Name?.ToLowerInvariant() switch
        {
            "poster" => ImageType.Primary,
            "banner" => ImageType.Banner,
            "background" => ImageType.Backdrop,
            "clearlogo" => ImageType.Logo,
            "clearart" => ImageType.Art,
            _ => null,
        };
    }

    /// <summary>
    /// Creates a <see cref="RemoteImageInfo"/> from an <see cref="EpisodeExtendedRecord"/>.
    /// </summary>
    /// <param name="episodeRecord">The <see cref="EpisodeExtendedRecord"/>.</param>
    /// <param name="providerName">The provider name.</param>
    /// <returns>A <see cref="RemoteImageInfo"/>, or null if <see cref="EpisodeExtendedRecord"/> does not contain image information.</returns>
    public static RemoteImageInfo? CreateImageInfo(this EpisodeExtendedRecord episodeRecord, string providerName)
    {
        if (string.IsNullOrEmpty(episodeRecord.Image))
        {
            return null;
        }

        return new RemoteImageInfo
        {
            ProviderName = providerName,
            Url = episodeRecord.Image,
            Type = ImageType.Primary
        };
    }

    /// <summary>
    /// Creates a <see cref="RemoteImageInfo"/> from an <see cref="ArtworkExtendedRecord"/>.
    /// </summary>
    /// <param name="artworkRecord">The <see cref="ArtworkExtendedRecord"/>.</param>
    /// <param name="providerName">The provider name.</param>
    /// <param name="type">The <see cref="ImageType"/>.</param>
    /// <param name="language">The <see cref="Language"/>.</param>
    /// <returns>A <see cref="RemoteImageInfo"/>, or null if <see cref="ImageType"/> is <see langword="null"/>.</returns>
    public static RemoteImageInfo? CreateImageInfo(this ArtworkExtendedRecord artworkRecord, string providerName, ImageType? type, Language? language)
    {
        return CreateRemoteImageInfo(
            artworkRecord.Image,
            artworkRecord.Thumbnail,
            (artworkRecord.Width, artworkRecord.Height),
            providerName,
            type,
            language);
    }

    /// <summary>
    /// Creates a <see cref="RemoteImageInfo"/> from an <see cref="ArtworkExtendedRecord"/>.
    /// </summary>
    /// <param name="artworkRecord">The <see cref="ArtworkExtendedRecord"/>.</param>
    /// <param name="providerName">The provider name.</param>
    /// <param name="type">The <see cref="ImageType"/>.</param>
    /// <param name="language">The <see cref="Language"/>.</param>
    /// <returns>A <see cref="RemoteImageInfo"/>, or null if <see cref="ImageType"/> is <see langword="null"/>.</returns>
    public static RemoteImageInfo? CreateImageInfo(this ArtworkBaseRecord artworkRecord, string providerName, ImageType? type, Language? language)
    {
        return CreateRemoteImageInfo(
            artworkRecord.Image,
            artworkRecord.Thumbnail,
            (artworkRecord.Width, artworkRecord.Height),
            providerName,
            type,
            language);
    }

    private static RemoteImageInfo? CreateRemoteImageInfo(string imageUrl, string thumbnailUrl, (long? Width, long? Height) imageDimension, string providerName, ImageType? type, Language? language)
    {
        if (type is null)
        {
            return null;
        }

        return new RemoteImageInfo
        {
            RatingType = RatingType.Score,
            Url = imageUrl,
            Width = Convert.ToInt32(imageDimension.Width, CultureInfo.InvariantCulture),
            Height = Convert.ToInt32(imageDimension.Height, CultureInfo.InvariantCulture),
            Type = type.Value,
            Language = language.NormalizeToJellyfin()?.ToLowerInvariant(),
            ProviderName = providerName,
            ThumbnailUrl = thumbnailUrl
        };
    }
}
