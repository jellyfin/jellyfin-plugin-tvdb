using System.Collections;
using System.Collections.Generic;

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
}
