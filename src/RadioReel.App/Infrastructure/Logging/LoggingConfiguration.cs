using Serilog;

namespace RadioReel.App.Infrastructure.Logging;

public static class LoggingConfiguration
{
    // TODO Task 2: replace hardcoded path with AppPaths.LogFile
    public static ILogger CreateLogger() =>
        new LoggerConfiguration()
            .WriteTo.File(
                "logs/radioreel-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();
}
