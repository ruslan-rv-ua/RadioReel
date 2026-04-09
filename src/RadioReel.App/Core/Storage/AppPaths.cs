using System.IO;

namespace RadioReel.App.Core.Storage;

public static class AppPaths
{
    public static string BaseDir => AppContext.BaseDirectory;

    public static string SettingsFile =>
        Path.Combine(BaseDir, "radioreel_settings.json");

    public static string LogsDir =>
        Path.Combine(BaseDir, "logs");

    public static string LogFile =>
        Path.Combine(LogsDir, "radioreel.log");

    public static string DefaultRecordingsDir =>
        Path.Combine(BaseDir, "recordings");
}
