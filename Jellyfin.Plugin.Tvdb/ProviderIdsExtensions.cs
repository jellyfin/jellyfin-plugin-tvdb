using System;
using System.Globalization;

using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.Tvdb;

internal static class ProviderIdsExtensions
{
    /// <summary>
    /// Check whether an item includes an entry for any supported provider IDs.
    /// </summary>
    /// <param name="item">The <see cref="IHasProviderIds"/>.</param>
    /// <returns>True, if <paramref name="item"/> contains any supported provider IDs.</returns>
    internal static bool IsSupported(this IHasProviderIds? item)
    {
        return HasProviderId(item, MetadataProvider.Tvdb)
               || HasProviderId(item, MetadataProvider.Imdb)
               || HasProviderId(item, MetadataProvider.Zap2It);
    }

    /// <summary>
    /// Get the TvDB id stored within the item.
    /// </summary>
    /// <param name="item">The <see cref="IHasProviderIds"/> item to get the TvDB id from.</param>
    /// <returns>The Id, or 0.</returns>
    public static int GetTvdbId(this IHasProviderIds item)
        => Convert.ToInt32(item.GetProviderId(TvdbPlugin.ProviderId), CultureInfo.InvariantCulture);

    /// <inheritdoc cref="SetTvdbId(IHasProviderIds, string?)" />
    public static void SetTvdbId(this IHasProviderIds item, long? value)
        => SetTvdbId(item, value.HasValue && value > 0 ? value.Value.ToString(CultureInfo.InvariantCulture) : null);

    /// <summary>
    /// Set the TvDB id in the item, if provided <paramref name="value"/> is not <see langword="null"/> or white space.
    /// </summary>
    /// <param name="item">>The <see cref="IHasProviderIds"/> to set the TvDB id.</param>
    /// <param name="value">TvDB id to set.</param>
    /// <returns><see langword="true"/> if value was set.</returns>
    public static bool SetTvdbId(this IHasProviderIds item, string? value)
        => SetProviderIdIfHasValue(item, TvdbPlugin.ProviderId, value);

    /// <inheritdoc cref="SetProviderIdIfHasValue(IHasProviderIds, string, string?)"/>
    public static bool SetProviderIdIfHasValue(this IHasProviderIds item, MetadataProvider provider, string? value)
        => SetProviderIdIfHasValue(item, provider.ToString(), value);

    /// <summary>
    /// Set the provider id in the item, if provided <paramref name="value"/> is not <see langword="null"/> or white space.
    /// </summary>
    /// <param name="item">>The <see cref="IHasProviderIds"/> to set the TvDB id.</param>
    /// <param name="name">Provider name.</param>
    /// <param name="value">Provider id to set.</param>
    /// <returns><see langword="true"/> if value was set.</returns>
    public static bool SetProviderIdIfHasValue(this IHasProviderIds item, string name, string? value)
    {
        if (!HasValue(value))
        {
            return false;
        }

        item.SetProviderId(name, value);
        return true;
    }

    /// <summary>
    /// Checks whether the item has TvDB Id stored.
    /// </summary>
    /// <param name="item">The <see cref="IHasProviderIds"/> item.</param>
    /// <returns>True, if item has TvDB Id stored.</returns>
    public static bool HasTvdbId(this IHasProviderIds? item)
        => HasTvdbId(item, out var value);

    /// <inheritdoc cref="HasProviderId(IHasProviderIds?, string, out string?)"/>
    public static bool HasTvdbId(this IHasProviderIds? item, out string? value)
        => HasProviderId(item, TvdbPlugin.ProviderId, out value);

    /// <inheritdoc cref="HasProviderId(IHasProviderIds?, string)"/>
    public static bool HasProviderId(this IHasProviderIds? item, MetadataProvider provider)
        => HasProviderId(item, provider, out var value);

    /// <inheritdoc cref="HasProviderId(IHasProviderIds?, string, out string?)"/>
    public static bool HasProviderId(this IHasProviderIds? item, MetadataProvider provider, out string? value)
        => HasProviderId(item, provider.ToString(), out value);

    /// <summary>
    /// Checks whether the item has provider id stored.
    /// </summary>
    /// <param name="item">The <see cref="IHasProviderIds"/> item.</param>
    /// <param name="name">Provider.</param>
    /// <returns>True, if item has provider id  stored.</returns>
    public static bool HasProviderId(this IHasProviderIds? item, string name)
        => HasProviderId(item, name, out var value);

    /// <summary>
    /// Checks whether the item has provider id stored.
    /// </summary>
    /// <param name="item">The <see cref="IHasProviderIds"/> item.</param>
    /// <param name="name">Provider.</param>
    /// <param name="value">The current provider id value.</param>
    /// <returns>True, if item has provider id stored.</returns>
    public static bool HasProviderId(this IHasProviderIds? item, string name, out string? value)
    {
        value = null;
        var result = item is { }
            && item.TryGetProviderId(name, out value)
            && HasValue(value);

        value = result ? value : null;
        return result;
    }

    private static bool HasValue(string? value)
        => !string.IsNullOrWhiteSpace(value);
}
