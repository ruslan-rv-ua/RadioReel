using RadioReel.App.Core.Models;

namespace RadioReel.App.Core.Storage;

public class AppSettings
{
    public GeneralSettings General { get; set; } = new();
    public RecordingSettings Recording { get; set; } = new();
    public List<StreamEntry> Streams { get; set; } = new();
}

public class GeneralSettings
{
    public double LowDiskSpaceWarningGb { get; set; } = 1.0;
}

public class RecordingSettings
{
    public string DefaultOutputDir { get; set; } = "recordings";
    public string FileNameTemplate { get; set; } = @"%s\%a - %t";
    public string IncompleteFileNameTemplate { get; set; } = @"%s\%a - %t_incomplete";
    public string StreamFileNameTemplate { get; set; } = "%s_stream_%d";
    public int SkipShortTracksMs { get; set; } = 30000;
    public int MaxReconnectAttempts { get; set; }
    public int ReconnectIntervalSec { get; set; } = 5;
}
