using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Tvdb.Configuration;
using MediaBrowser.Common;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.DependencyInjection;
using Tvdb.Sdk;

namespace Jellyfin.Plugin.Tvdb;

/// <summary>
/// Tvdb client manager.
/// </summary>
public class TvdbClientManager
{
    private const string TvdbHttpClient = "TvdbHttpClient";
    private static readonly SemaphoreSlim _tokenUpdateLock = new SemaphoreSlim(1, 1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceProvider _serviceProvider;
    private readonly SdkClientSettings _sdkClientSettings;

    private DateTime _tokenUpdatedAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="TvdbClientManager"/> class.
    /// </summary>
    /// <param name="applicationHost">Instance of the <see cref="IApplicationHost"/> interface.</param>
    public TvdbClientManager(IApplicationHost applicationHost)
    {
        _serviceProvider = ConfigureService(applicationHost);
        _httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
        _sdkClientSettings = _serviceProvider.GetRequiredService<SdkClientSettings>();

        _tokenUpdatedAt = DateTime.MinValue;
    }

    private static string? UserPin => TvdbPlugin.Instance?.Configuration.ApiKey;

    /// <summary>
    /// Logs in or refresh login to the tvdb api when needed.
    /// </summary>
    private async Task LoginAsync()
    {
        var loginClient = _serviceProvider.GetRequiredService<ILoginClient>();

        // Ensure we have a recent token.
        if (IsTokenInvalid())
        {
            await _tokenUpdateLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (IsTokenInvalid())
                {
                    var loginResponse = await loginClient.LoginAsync(new Body
                    {
                        Apikey = PluginConfiguration.ProjectApiKey,
                        Pin = UserPin
                    }).ConfigureAwait(false);

                    _tokenUpdatedAt = DateTime.UtcNow;
                    _sdkClientSettings.AccessToken = loginResponse.Data.Token;
                }
            }
            finally
            {
                _tokenUpdateLock.Release();
            }
        }

        return;

        bool IsTokenInvalid() =>
            _tokenUpdatedAt == DateTime.MinValue
            || string.IsNullOrEmpty(_sdkClientSettings.AccessToken)
            || _tokenUpdatedAt < DateTime.UtcNow.Subtract(TimeSpan.FromDays(25));
    }

    /// <summary>
    /// Get series by name.
    /// </summary>
    /// <param name="name">Series name.</param>
    /// <param name="language">Metadata language.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The series search result.</returns>
    public async Task<IReadOnlyList<SearchResult>> GetSeriesByNameAsync(
        string name,
        string language,
        CancellationToken cancellationToken)
    {
        var searchClient = _serviceProvider.GetRequiredService<ISearchClient>();
        await LoginAsync().ConfigureAwait(false);
        var searchResult = await searchClient.GetSearchResultsAsync(query: name, type: "series", limit: 5, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return searchResult.Data;
    }

    /// <summary>
    /// Get series by id.
    /// </summary>
    /// <param name="tvdbId">The series tvdb id.</param>
    /// <param name="language">Metadata language.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The series response.</returns>
    public async Task<SeriesBaseRecord> GetSeriesByIdAsync(
        int tvdbId,
        string language,
        CancellationToken cancellationToken)
    {
        var seriesClient = _serviceProvider.GetRequiredService<ISeriesClient>();
        await LoginAsync().ConfigureAwait(false);
        var seriesResult = await seriesClient.GetSeriesBaseAsync(id: tvdbId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return seriesResult.Data;
    }

    /// <summary>
    /// Get series by id.
    /// </summary>
    /// <param name="tvdbId">The series tvdb id.</param>
    /// <param name="language">Metadata language.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="meta">episodes or translations.</param>
    /// <param name="small">Payload size. True for smaller payload.</param>
    /// <returns>The series response.</returns>
    public async Task<SeriesExtendedRecord> GetSeriesExtendedByIdAsync(
        int tvdbId,
        string language,
        CancellationToken cancellationToken,
        Meta4? meta = null,
        bool? small = null)
    {
        var seriesClient = _serviceProvider.GetRequiredService<ISeriesClient>();
        await LoginAsync().ConfigureAwait(false);
        var seriesResult = await seriesClient.GetSeriesExtendedAsync(id: tvdbId, meta: meta, @short: small, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return seriesResult.Data;
    }

    /// <summary>
    /// Get all episodes of series.
    /// </summary>
    /// <param name="tvdbId">The series tvdb id.</param>
    /// <param name="language">Metadata language.</param>
    /// <param name="seasonType">Season type: default, dvd, absolute etc.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All episodes of series.</returns>
    public async Task<Data2> GetSeriesEpisodesAsync(
        int tvdbId,
        string language,
        string seasonType,
        CancellationToken cancellationToken)
    {
        var seriesClient = _serviceProvider.GetRequiredService<ISeriesClient>();
        await LoginAsync().ConfigureAwait(false);
        var seriesResult = await seriesClient.GetSeriesEpisodesAsync(id: tvdbId, season_type: seasonType, cancellationToken: cancellationToken, page: 0)
            .ConfigureAwait(false);
        return seriesResult.Data;
    }

    /// <summary>
    /// Get Season record.
    /// </summary>
    /// <param name="seasonTvdbId">The season tvdb id.</param>
    /// <param name="language">Metadata language.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The episode record.</returns>
    public async Task<SeasonExtendedRecord> GetSeasonByIdAsync(
        int seasonTvdbId,
        string language,
        CancellationToken cancellationToken)
    {
        var seasonClient = _serviceProvider.GetRequiredService<ISeasonsClient>();
        await LoginAsync().ConfigureAwait(false);
        var seasonResult = await seasonClient.GetSeasonExtendedAsync(id: seasonTvdbId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return seasonResult.Data;
    }

    /// <summary>
    /// Get episode record.
    /// </summary>
    /// <param name="episodeTvdbId">The episode tvdb id.</param>
    /// <param name="language">Metadata language.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The episode record.</returns>
    public async Task<EpisodeExtendedRecord> GetEpisodesAsync(
        int episodeTvdbId,
        string language,
        CancellationToken cancellationToken)
    {
        var episodeClient = _serviceProvider.GetRequiredService<IEpisodesClient>();
        await LoginAsync().ConfigureAwait(false);
        var episodeResult = await episodeClient.GetEpisodeExtendedAsync(id: episodeTvdbId, meta: Meta.Translations, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return episodeResult.Data;
    }

    /// <summary>
    /// Get series by remoteId.
    /// </summary>
    /// <param name="remoteId">The remote id. Supported RemoteIds are: IMDB, TMDB, Zap2It, TV Maze and EIDR.</param>
    /// <param name="language">Metadata language.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The series search result.</returns>
    public async Task<IReadOnlyList<SearchByRemoteIdResult>> GetSeriesByRemoteIdAsync(
        string remoteId,
        string language,
        CancellationToken cancellationToken)
    {
        var searchClient = _serviceProvider.GetRequiredService<ISearchClient>();
        await LoginAsync().ConfigureAwait(false);
        var searchResult = await searchClient.GetSearchResultsByRemoteIdAsync(remoteId: remoteId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return searchResult.Data;
    }

    /// <summary>
    /// Get actors by tvdb id.
    /// </summary>
    /// <param name="tvdbId">People Tvdb id.</param>
    /// <param name="language">Metadata language.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The actors attached to the id.</returns>
    public async Task<PeopleBaseRecord> GetActorAsync(
        int tvdbId,
        string language,
        CancellationToken cancellationToken)
    {
        var peopleClient = _serviceProvider.GetRequiredService<IPeopleClient>();
        await LoginAsync().ConfigureAwait(false);
        var peopleResult = await peopleClient.GetPeopleBaseAsync(id: tvdbId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return peopleResult.Data;
    }

    /// <summary>
    /// Get image by image tvdb id.
    /// </summary>
    /// <param name="imageTvdbId"> Tvdb id.</param>
    /// <param name="language">Metadata language.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The images attached to the id.</returns>
    public async Task<ArtworkExtendedRecord> GetImageAsync(
        int imageTvdbId,
        string language,
        CancellationToken cancellationToken)
    {
        var artworkClient = _serviceProvider.GetRequiredService<IArtworkClient>();
        await LoginAsync().ConfigureAwait(false);
        var artworkResult = await artworkClient.GetArtworkExtendedAsync(id: imageTvdbId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return artworkResult.Data;
    }

    /// <summary>
    /// Get image by series tvdb id.
    /// </summary>
    /// <param name="tvdbId"> Tvdb id.</param>
    /// <param name="language">Metadata language.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The images attached to the id.</returns>
    public async Task<SeriesExtendedRecord> GetSeriesImagesAsync(
        int tvdbId,
        string language,
        CancellationToken cancellationToken)
    {
        var seriesClient = _serviceProvider.GetRequiredService<ISeriesClient>();
        await LoginAsync().ConfigureAwait(false);
        var seriesResult = await seriesClient.GetSeriesArtworksAsync(id: tvdbId, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return seriesResult.Data;
    }

    /// <summary>
    /// Get all tvdb languages.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All tvdb languages.</returns>
    public async Task<IReadOnlyList<Language>> GetLanguagesAsync(CancellationToken cancellationToken)
    {
        var languagesClient = _serviceProvider.GetRequiredService<ILanguagesClient>();
        await LoginAsync().ConfigureAwait(false);
        var languagesResult = await languagesClient.GetAllLanguagesAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return languagesResult.Data;
    }

    /// <summary>
    /// Gets all tvdb artwork types.
    /// </summary>
    /// <param name="cancellationToken">Cancellation Token.</param>
    /// <returns>All tvdb artwork types.</returns>
    public async Task<IReadOnlyList<ArtworkType>> GetArtworkTypeAsync(CancellationToken cancellationToken)
    {
        var artworkTypesClient = _serviceProvider.GetRequiredService<IArtwork_TypesClient>();
        await LoginAsync().ConfigureAwait(false);
        var artworkTypesResult = await artworkTypesClient.GetAllArtworkTypesAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return artworkTypesResult.Data;
    }

    /// <summary>
    /// Get an episode's tvdb id.
    /// </summary>
    /// <param name="searchInfo">Episode search info.</param>
    /// <param name="language">Metadata language.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tvdb id.</returns>
    public async Task<string?> GetEpisodeTvdbId(
        EpisodeInfo searchInfo,
        string language,
        CancellationToken cancellationToken)
    {
        var seriesClient = _serviceProvider.GetRequiredService<ISeriesClient>();
        await LoginAsync().ConfigureAwait(false);
        searchInfo.SeriesProviderIds.TryGetValue(TvdbPlugin.ProviderId, out var seriesTvdbId);
        int? episodeNumber = null;
        int? seasonNumber = null;
        string? airDate = null;
        bool special = false;
        // Prefer SxE over premiere date as it is more robust
        if (searchInfo.IndexNumber.HasValue && searchInfo.ParentIndexNumber.HasValue)
        {
            switch (searchInfo.SeriesDisplayOrder)
            {
                case "dvd":
                    episodeNumber = searchInfo.IndexNumber.Value;
                    seasonNumber = searchInfo.ParentIndexNumber.Value;
                    break;
                case "absolute":
                    if (searchInfo.ParentIndexNumber.Value == 0) // check if special
                    {
                        special = true;
                        seasonNumber = 0;
                    }
                    else
                    {
                        seasonNumber = 1; // absolute order is always season 1
                    }

                    episodeNumber = searchInfo.IndexNumber.Value;
                    break;
                default:
                    // aired order
                    episodeNumber = searchInfo.IndexNumber.Value;
                    seasonNumber = searchInfo.ParentIndexNumber.Value;
                    break;
            }
        }
        else if (searchInfo.PremiereDate.HasValue)
        {
            // tvdb expects yyyy-mm-dd format
            airDate = searchInfo.PremiereDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        Response56 seriesResponse;
        if (!special)
        {
            switch (searchInfo.SeriesDisplayOrder)
            {
                case "dvd":
                case "absolute":
                    seriesResponse = await seriesClient.GetSeriesEpisodesAsync(page: 0, id: Convert.ToInt32(seriesTvdbId, CultureInfo.InvariantCulture), season_type: searchInfo.SeriesDisplayOrder, season: seasonNumber, episodeNumber: episodeNumber, airDate: airDate, cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    seriesResponse = await seriesClient.GetSeriesEpisodesAsync(page: 0, id: Convert.ToInt32(seriesTvdbId, CultureInfo.InvariantCulture), season_type: "default", season: seasonNumber, episodeNumber: episodeNumber, airDate: airDate, cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;
            }
        }
        else // when special use default order
        {
            seriesResponse = await seriesClient.GetSeriesEpisodesAsync(page: 0, id: Convert.ToInt32(seriesTvdbId, CultureInfo.InvariantCulture), season_type: "default", season: seasonNumber, episodeNumber: episodeNumber, airDate: airDate, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        Data2 seriesData = seriesResponse.Data;

        if (seriesData == null || seriesData.Episodes == null || seriesData.Episodes.Count == 0 || seriesData.Episodes[0].Id.HasValue == false)
        {
            return null;
        }
        else
        {
            return seriesData.Episodes[0].Id.GetValueOrDefault().ToString(CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Create an independent ServiceProvider because registering HttpClients directly into Jellyfin
    /// causes issues upstream.
    /// </summary>
    /// <param name="applicationHost">Instance of the <see cref="IApplicationHost"/>.</param>
    /// <returns>The service provider.</returns>
    private ServiceProvider ConfigureService(IApplicationHost applicationHost)
    {
        var productHeader = ProductInfoHeaderValue.Parse(applicationHost.ApplicationUserAgent);

        var assembly = typeof(TvdbPlugin).Assembly.GetName();
        var pluginHeader = new ProductInfoHeaderValue(
            assembly.Name!.Replace(' ', '-').Replace('.', '-'),
            assembly.Version!.ToString(3));

        var contactHeader = new ProductInfoHeaderValue($"({applicationHost.ApplicationUserAgentAddress})");

        var services = new ServiceCollection();

        services.AddSingleton<SdkClientSettings>();
        services.AddHttpClient(TvdbHttpClient, c =>
            {
                c.DefaultRequestHeaders.UserAgent.Add(productHeader);
                c.DefaultRequestHeaders.UserAgent.Add(pluginHeader);
                c.DefaultRequestHeaders.UserAgent.Add(contactHeader);
            })
            .ConfigurePrimaryHttpMessageHandler(_ => new SocketsHttpHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
                RequestHeaderEncodingSelector = (_, _) => Encoding.UTF8
            });

        services.AddTransient<ILoginClient>(_ => new LoginClient(_sdkClientSettings, _httpClientFactory.CreateClient(TvdbHttpClient)));
        services.AddTransient<ISearchClient>(_ => new SearchClient(_sdkClientSettings, _httpClientFactory.CreateClient(TvdbHttpClient)));
        services.AddTransient<ISeriesClient>(_ => new SeriesClient(_sdkClientSettings, _httpClientFactory.CreateClient(TvdbHttpClient)));
        services.AddTransient<ISeasonsClient>(_ => new SeasonsClient(_sdkClientSettings, _httpClientFactory.CreateClient(TvdbHttpClient)));
        services.AddTransient<IEpisodesClient>(_ => new EpisodesClient(_sdkClientSettings, _httpClientFactory.CreateClient(TvdbHttpClient)));
        services.AddTransient<IPeopleClient>(_ => new PeopleClient(_sdkClientSettings, _httpClientFactory.CreateClient(TvdbHttpClient)));
        services.AddTransient<IArtworkClient>(_ => new ArtworkClient(_sdkClientSettings, _httpClientFactory.CreateClient(TvdbHttpClient)));
        services.AddTransient<IArtwork_TypesClient>(_ => new Artwork_TypesClient(_sdkClientSettings, _httpClientFactory.CreateClient(TvdbHttpClient)));
        services.AddTransient<ILanguagesClient>(_ => new LanguagesClient(_sdkClientSettings, _httpClientFactory.CreateClient(TvdbHttpClient)));

        return services.BuildServiceProvider();
    }
}
