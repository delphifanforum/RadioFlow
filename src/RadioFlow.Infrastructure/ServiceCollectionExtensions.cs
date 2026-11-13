using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadioFlow.Core.Constants;
using RadioFlow.Core.Interfaces;
using RadioFlow.Infrastructure.Audio;
using RadioFlow.Infrastructure.Persistence;
using RadioFlow.Infrastructure.RadioBrowser;
using RadioFlow.Infrastructure.Security;
using RadioFlow.Infrastructure.Services;

namespace RadioFlow.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>Altyapı servislerini kaydeder. Ses motoru ayrı kaydedilir (<see cref="AddLibVlcAudioEngine"/>).</summary>
    public static IServiceCollection AddRadioFlowInfrastructure(this IServiceCollection services, IAppPaths paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        services.AddSingleton(paths);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IStreamUrlValidator, StreamUrlValidator>();

        // Kalıcılık
        services.AddSingleton(new SqliteConnectionFactory(paths.DatabasePath));
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<IStationRepository, StationRepository>();
        services.AddSingleton<IFavoritesRepository, FavoritesRepository>();
        services.AddSingleton<IHistoryRepository, HistoryRepository>();
        services.AddSingleton<ISettingsStore, SettingsStore>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ILibraryService, LibraryService>();

        // HTTP: IHttpClientFactory ile yönetilen, zaman aşımlı ve yönlendirmesi sınırlı istemciler.
        services.AddSingleton<IRadioBrowserServerProvider, RadioBrowserServerProvider>();
        services.AddHttpClient(HttpClientNames.Catalog, ConfigureClient).ConfigurePrimaryHttpMessageHandler(CreateHandler);
        services.AddHttpClient(HttpClientNames.Playlist, ConfigureClient).ConfigurePrimaryHttpMessageHandler(CreateHandler);
        services.AddHttpClient(HttpClientNames.Logos, ConfigureClient).ConfigurePrimaryHttpMessageHandler(CreateHandler);

        // Uzun ömürlü servisler; SocketsHttpHandler.PooledConnectionLifetime sayesinde DNS değişiklikleri izlenir.
        services.AddSingleton<IRadioCatalogService>(sp => ActivatorUtilities.CreateInstance<RadioBrowserClient>(
            sp, sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientNames.Catalog)));
        services.AddSingleton<IPlaylistResolver>(sp => ActivatorUtilities.CreateInstance<PlaylistResolver>(
            sp, sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientNames.Playlist)));
        services.AddSingleton<ILogoCache>(sp => ActivatorUtilities.CreateInstance<LogoCache>(
            sp, sp.GetRequiredService<IHttpClientFactory>().CreateClient(HttpClientNames.Logos)));

        services.AddSingleton<IRadioPlayerService>(sp => new RadioPlayerService(
            sp.GetRequiredService<IAudioEngine>(),
            sp.GetRequiredService<IPlaylistResolver>(),
            sp.GetRequiredService<IStreamUrlValidator>(),
            () => sp.GetRequiredService<ISettingsService>().Current.ToPlaybackOptions(),
            sp.GetRequiredService<ILogger<RadioPlayerService>>(),
            sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IPlaybackHistoryTracker, PlaybackHistoryTracker>();

        return services;
    }

    public static IServiceCollection AddLibVlcAudioEngine(this IServiceCollection services)
    {
        services.AddSingleton<IAudioEngine, LibVlcAudioEngine>();
        return services;
    }

    internal static class HttpClientNames
    {
        public const string Catalog = "RadioBrowser";
        public const string Playlist = "Playlist";
        public const string Logos = "Logos";
    }

    private static void ConfigureClient(HttpClient client)
    {
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(AppConstants.UserAgent);
    }

    private static SocketsHttpHandler CreateHandler() => new()
    {
        AllowAutoRedirect = true,
        MaxAutomaticRedirections = 5,
        AutomaticDecompression = DecompressionMethods.All,
        ConnectTimeout = TimeSpan.FromSeconds(8),
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        UseCookies = false,
    };
}
