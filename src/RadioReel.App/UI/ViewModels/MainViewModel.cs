using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace RadioReel.App.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _connectionStatus = "Відключено";

    [ObservableProperty]
    private int _activeRecordingsCount;

    [ObservableProperty]
    private string _freeDiskSpace = "";

    public StreamsViewModel Streams { get; }

    public MainViewModel(StreamsViewModel streamsViewModel)
    {
        Streams = streamsViewModel;
        UpdateFreeDiskSpace();
    }

    public void UpdateFreeDiskSpace()
    {
        try
        {
            var drive = new DriveInfo(Path.GetPathRoot(Core.Storage.AppPaths.BaseDir)!);
            var gb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            FreeDiskSpace = $"{gb:F1} ГБ вільно";
        }
        catch
        {
            FreeDiskSpace = "—";
        }
    }

    public Task ShutdownAsync()
    {
        // Will be filled in Task 9 when RecordingSession is wired
        return Task.CompletedTask;
    }
}
