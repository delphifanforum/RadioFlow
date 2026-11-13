using Serilog;
using Serilog.Events;

namespace RadioFlow.Infrastructure.Logging;

public static class LoggingSetup
{
    /// <summary>
    /// %LOCALAPPDATA%\RadioFlow\Logs altında günlük dönen (rolling) dosya logu oluşturur.
    /// Dosya başına 10 MB sınır, en fazla 14 dosya saklanır.
    /// </summary>
    public static ILogger CreateLogger(string logsDirectory, bool verbose = false)
    {
        Directory.CreateDirectory(logsDirectory);
        return new LoggerConfiguration()
            .MinimumLevel.Is(verbose ? LogEventLevel.Debug : LogEventLevel.Information)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System.Net.Http.HttpClient", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(logsDirectory, "radioflow-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                shared: false,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }
}
