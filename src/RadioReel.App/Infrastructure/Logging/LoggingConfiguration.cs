using System.IO;
using RadioReel.App.Core.Storage;
using Serilog;

namespace RadioReel.App.Infrastructure.Logging;

public static class LoggingConfiguration
{
    public static ILogger CreateLogger()
    {
        Directory.CreateDirectory(AppPaths.LogsDir);

        return new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .WriteTo.File(
                AppPaths.LogFile,
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{SourceContext}] {Message}{NewLine}{Exception}",
                fileSizeLimitBytes: 10 * 1024 * 1024,
                retainedFileCountLimit: 3,
                rollOnFileSizeLimit: true)
            .CreateLogger();
    }
}
