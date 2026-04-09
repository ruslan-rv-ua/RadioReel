using System.IO;
using System.Text.Json;
using Serilog;

namespace RadioReel.App.Core.Storage;

public sealed class SettingsStore : ISettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;

    public SettingsStore() : this(AppPaths.SettingsFile) { }

    public SettingsStore(string filePath)
    {
        _filePath = filePath;
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new AppSettings();

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                   ?? new AppSettings();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load settings from {Path}, using defaults", _filePath);
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (dir is not null)
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            var tempPath = _filePath + ".tmp";
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings to {Path}", _filePath);
        }
    }
}
