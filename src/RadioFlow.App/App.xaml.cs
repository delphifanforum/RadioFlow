using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RadioFlow.App.Services;
using RadioFlow.App.ViewModels;
using RadioFlow.App.Views;
using RadioFlow.Core.Interfaces;
using RadioFlow.Infrastructure;
using RadioFlow.Infrastructure.Logging;
using RadioFlow.Infrastructure.Persistence;
using RadioFlow.Infrastructure.WindowsIntegration;
using Serilog;

namespace RadioFlow.App;

public partial class App : Application
{
    private IHost? _host;
    private SingleInstanceGuard? _instanceGuard;

    /// <summary>Uygulama "--startup" argümanıyla (Windows oturum açılışı) başlatıldı mı?</summary>
    public static bool LaunchedAtStartup { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LaunchedAtStartup = e.Args.Contains(RegistryStartupService.StartupArgument, StringComparer.OrdinalIgnoreCase);

        _instanceGuard = new SingleInstanceGuard();
        if (!_instanceGuard.IsFirstInstance)
        {
            // Zaten çalışıyor: mevcut örneği öne getir ve çık.
            _instanceGuard.SignalFirstInstance();
            _instanceGuard.Dispose();
            _instanceGuard = null;
            Shutdown(0);
            return;
        }

        var paths = new AppPaths();
        Log.Logger = LoggingSetup.CreateLogger(paths.LogsDirectory);
        Log.Information("RadioFlow {Version} başlatılıyor. OS={OS}, .NET={Runtime}",
            typeof(App).Assembly.GetName().Version, Environment.OSVersion, Environment.Version);

        RegisterGlobalExceptionHandlers();

        try
        {
            _host = BuildHost(paths);
            await _host.StartAsync();
            var services = _host.Services;

            await services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
            var settings = await services.GetRequiredService<ISettingsService>().LoadAsync();

            services.GetRequiredService<ThemeService>().Apply(settings.Theme);
            services.GetRequiredService<IPlaybackHistoryTracker>().Start();

            var player = services.GetRequiredService<PlayerViewModel>();
            player.InitializeFromSettings();

            var shell = services.GetRequiredService<ShellService>();
            shell.Initialize();
            _instanceGuard.ListenForActivation(() => Dispatcher.InvokeAsync(shell.ShowWidget));

            await player.RestoreLastStationAsync(settings.AutoPlayLastStation);
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Uygulama başlatılamadı.");
            MessageBox.Show(
                "RadioFlow başlatılamadı. Ayrıntılar log dosyasına yazıldı:\n" + paths.LogsDirectory,
                "RadioFlow",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            await ShutdownCoreAsync();
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("RadioFlow kapandı (kod {Code}).", e.ApplicationExitCode);
        Log.CloseAndFlush();
        _instanceGuard?.Dispose();
        base.OnExit(e);
    }

    /// <summary>Tüm servisleri düzgün kapatır (ShellService.ExitAsync tarafından çağrılır).</summary>
    internal async Task ShutdownCoreAsync()
    {
        if (_host is null)
        {
            return;
        }

        try
        {
            await _host.StopAsync(TimeSpan.FromSeconds(3));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Host durdurulurken hata.");
        }

        // Singleton'lar (LibVLC, tepsi simgesi, SMTC) burada Dispose edilir.
        if (_host is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _host.Dispose();
        }

        _host = null;
    }

    private static IHost BuildHost(AppPaths paths)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings
        {
            ApplicationName = "RadioFlow",
        });

        builder.Services.AddSerilog(Log.Logger, dispose: false);
        builder.Services.AddRadioFlowInfrastructure(paths);
        builder.Services.AddLibVlcAudioEngine();

        // Windows entegrasyonu
        builder.Services.AddSingleton<RegistryStartupService>();
        builder.Services.AddSingleton<StartupServiceSelector>();
        builder.Services.AddSingleton<IStartupService>(sp => sp.GetRequiredService<StartupServiceSelector>());
        builder.Services.AddSingleton<IUiDispatcher>(_ => new WpfUiDispatcher(Current.Dispatcher));
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<TrayIconService>();
        builder.Services.AddSingleton<MediaControlsService>();
        builder.Services.AddSingleton<ShellService>();
        builder.Services.AddSingleton<IShellService>(sp => sp.GetRequiredService<ShellService>());

        // ViewModel'ler
        builder.Services.AddSingleton<PlayerViewModel>();
        builder.Services.AddSingleton<WidgetViewModel>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Pencereler (mini widget ve ana pencere tekil; gizlenip yeniden gösterilir)
        builder.Services.AddSingleton<MiniWidgetWindow>();
        builder.Services.AddSingleton<MainWindow>();
        builder.Services.AddTransient<SettingsWindow>();

        return builder.Build();
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Fatal(args.ExceptionObject as Exception, "İşlenmeyen istisna (AppDomain).");
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(args.Exception, "Gözlemlenmeyen görev istisnası.");
            args.SetObserved();
        };
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "İşlenmeyen UI istisnası.");
        e.Handled = true;
        // Kullanıcıya teknik ayrıntı (stack trace) gösterilmez.
        _host?.Services.GetService<IShellService>()?.ShowError(
            "Beklenmeyen bir hata oluştu. Uygulama çalışmaya devam ediyor; ayrıntılar log dosyasına kaydedildi.");
    }
}
