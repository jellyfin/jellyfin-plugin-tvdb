using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Model.Providers;

using Microsoft.Extensions.Logging;

using Tvdb.Sdk;

namespace Jellyfin.Plugin.Tvdb.Providers;

/// <summary>
/// Tvdb season image provider.
/// </summary>
public class TvdbSeasonImageProvider : IRemoteImageProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TvdbSeasonImageProvider> _logger;
    private readonly TvdbClientManager _tvdbClientManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TvdbSeasonImageProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
    /// <param name="logger">Instance of the <see cref="ILogger{TvdbSeasonImageProvider}"/> interface.</param>
    /// <param name="tvdbClientManager">Instance of <see cref="TvdbClientManager"/>.</param>
    public TvdbSeasonImageProvider(
        IHttpClientFactory httpClientFactory,
        ILogger<TvdbSeasonImageProvider> logger,
        TvdbClientManager tvdbClientManager)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _tvdbClientManager = tvdbClientManager;
    }

    /// <inheritdoc />
    public string Name => TvdbPlugin.ProviderName;

    /// <inheritdoc />
    public bool Supports(BaseItem item)
    {
        return item is Season;
    }

    /// <inheritdoc />
    public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
    {
        yield return ImageType.Primary;
        yield return ImageType.Banner;
        yield return ImageType.Backdrop;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
    {
        var season = (Season)item;
        var series = season.Series;

        if (!season.HasTvdbId() && (!series.IsSupported() || season.IndexNumber is null))
        {
            return Enumerable.Empty<RemoteImageInfo>();
        }

        var languages = await _tvdbClientManager.GetLanguagesAsync(cancellationToken)
            .ConfigureAwait(false);
        var languageLookup = languages
            .ToDictionary(l => l.Id, StringComparer.OrdinalIgnoreCase);

        var artworkTypes = await _tvdbClientManager.GetArtworkTypeAsync(cancellationToken)
            .ConfigureAwait(false);
        var seasonArtworkTypeLookup = artworkTypes
            .Where(t => string.Equals(t.RecordType, "season", StringComparison.OrdinalIgnoreCase))
            .Where(t => t.Id.HasValue)
            .ToDictionary(t => t.Id!.Value);

        var seriesTvdbId = series.GetTvdbId();
        var seasonTvdbId = season.GetTvdbId();
        var seasonNumber = season.IndexNumber ?? 0;
        var displayOrder = season.Series.DisplayOrder;

        if (string.IsNullOrEmpty(displayOrder))
        {
            displayOrder = "official";
        }

        var seasonArtworks = await GetSeasonArtworks(seasonTvdbId, seriesTvdbId, seasonNumber, displayOrder, cancellationToken)
            .ConfigureAwait(false);

        var remoteImages = new List<RemoteImageInfo>();
        foreach (var artwork in seasonArtworks)
        {
            var artworkType = artwork.Type is null ? null : seasonArtworkTypeLookup.GetValueOrDefault(artwork.Type!.Value);
            var imageType = artworkType.GetImageType();
            var artworkLanguage = artwork.Language is null ? null : languageLookup.GetValueOrDefault(artwork.Language);

            // only add if valid RemoteImageInfo
            remoteImages.AddIfNotNull(artwork.CreateImageInfo(Name, imageType, artworkLanguage));
        }

        return remoteImages.OrderByLanguageDescending(item.GetPreferredMetadataLanguage());
    }

    private async Task<IReadOnlyList<ArtworkBaseRecord>> GetSeasonArtworks(int seasonTvdbId, int seriesTvdbId, int seasonNumber, string displayOrder, CancellationToken cancellationToken)
    {
        try
        {
            var officialFallbackId = 0;

            // Do the series lookup if we need to find the season ID
            if (seasonTvdbId == 0)
            {
                var seasonsByNumber = await GetSeriesSeasonsByNumberAsync(seriesTvdbId, seasonNumber, cancellationToken)
                    .ConfigureAwait(false);

                seasonTvdbId = seasonsByNumber
                    .FirstOrDefault(s => s.Type.Type == displayOrder)?.Id ?? 0;

                officialFallbackId = seasonsByNumber
                    .FirstOrDefault(s => s.Type.Type == "official")?.Id ?? 0;
            }

            var seasonInfo = await _tvdbClientManager.GetSeasonByIdAsync(seasonTvdbId, string.Empty, cancellationToken)
                .ConfigureAwait(false);

            // If no images found for the requested display order, fall back to the official order
            if (seasonInfo.Artwork is null || seasonInfo.Artwork.Count == 0)
            {
                // lazy lookup of the official season ID if we didn't already have it from the initial lookup by season number and display order
                if (officialFallbackId == 0)
                {
                    var seasonsByNumber = await GetSeriesSeasonsByNumberAsync(seriesTvdbId, seasonNumber, cancellationToken)
                        .ConfigureAwait(false);
                    officialFallbackId = seasonsByNumber.FirstOrDefault(s => s.Type.Type == "official")?.Id ?? 0;
                }

                if (officialFallbackId != seasonTvdbId)
                {
                    seasonInfo = await _tvdbClientManager.GetSeasonByIdAsync(officialFallbackId, string.Empty, cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            if (seasonInfo.Artwork is not null)
            {
                return seasonInfo.Artwork;
            }
        }
        catch (Exception ex) when (
            (ex is SeriesException seriesEx && seriesEx.InnerException is JsonException)
            || (ex is SeasonsException seasonEx && seasonEx.InnerException is JsonException))
        {
            _logger.LogError(ex, "Failed to retrieve season images for series {TvDbId}", seriesTvdbId);
        }

        return Array.Empty<ArtworkBaseRecord>();
    }

    private async Task<IReadOnlyList<SeasonBaseRecord>> GetSeriesSeasonsByNumberAsync(int seriesTvdbId, int seasonNumber, CancellationToken cancellationToken)
    {
        var seriesInfo = await _tvdbClientManager.GetSeriesExtendedByIdAsync(seriesTvdbId, string.Empty, cancellationToken, small: true)
            .ConfigureAwait(false);

        return seriesInfo.Seasons
            .Where(s => s.Number == seasonNumber)
            .ToList();
    }

    /// <inheritdoc />
    public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
    {
        return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);
    }
}
