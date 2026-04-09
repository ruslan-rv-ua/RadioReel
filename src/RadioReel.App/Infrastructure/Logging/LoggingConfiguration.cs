using Serilog;

namespace RadioReel.App.Infrastructure.Logging;

internal static class LoggingConfiguration
{
    public static ILogger CreateLogger() =>
        new LoggerConfiguration()
            .WriteTo.File(
                "logs/radioreel-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();
}
