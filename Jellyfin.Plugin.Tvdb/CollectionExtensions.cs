using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Tvdb;

internal static class CollectionExtensions
{
    /// <summary>
    /// Adds the <paramref name="item"/> if it is not <see langword="null"/>.
    /// </summary>
    /// <typeparam name="T">Data type of the collection items.</typeparam>
    /// <param name="collection">Input <see cref="ICollection"/>.</param>
    /// <param name="item">Item to add.</param>
    /// <returns>The input collection.</returns>
    public static ICollection<T> AddIfNotNull<T>(this ICollection<T> collection, T? item)
    {
        if (item != null)
        {
            collection.Add(item);
        }

        return collection;
    }

    internal static IEnumerable<RemoteImageInfo> OrderByLanguageDescending(this IEnumerable<RemoteImageInfo> remoteImageInfos, string requestedLanguage)
    {
        if (string.IsNullOrWhiteSpace(requestedLanguage))
        {
            // Default to English if no requested language is specified.
            requestedLanguage = "en";
        }

        var isRequestedLanguageEn = string.Equals(requestedLanguage, "en", StringComparison.OrdinalIgnoreCase);

        return remoteImageInfos.OrderByDescending(i =>
        {
            if (string.Equals(requestedLanguage, i.Language, StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (string.IsNullOrEmpty(i.Language))
            {
                // Empty image language is image without any text
                return 2;
            }

            if (!isRequestedLanguageEn && string.Equals(i.Language, "en", StringComparison.OrdinalIgnoreCase))
            {
                // Prioritize English over non-requested languages.
                return 1;
            }

            return 0;
        });
    }
}
