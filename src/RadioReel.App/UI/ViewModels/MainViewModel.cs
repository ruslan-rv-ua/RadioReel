using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using RadioReel.App.Core.Storage;

namespace RadioReel.App.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DispatcherTimer _diskCheckTimer;

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

        Streams.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(StreamsViewModel.IsRecording))
            {
                ActiveRecordingsCount = Streams.IsRecording ? 1 : 0;
                ConnectionStatus = Streams.IsRecording ? "Запис" : "Відключено";
            }
        };

        UpdateFreeDiskSpace();

        _diskCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(60)
        };
        _diskCheckTimer.Tick += (_, _) => UpdateFreeDiskSpace();
        _diskCheckTimer.Start();
    }

    public void UpdateFreeDiskSpace()
    {
        try
        {
            var root = Path.GetPathRoot(AppPaths.BaseDir);
            if (root is null) { FreeDiskSpace = "—"; return; }

            var drive = new DriveInfo(root);
            var gb = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
            FreeDiskSpace = $"{gb:F1} ГБ вільно";
        }
        catch
        {
            FreeDiskSpace = "—";
        }
    }

    public async Task ShutdownAsync()
    {
        _diskCheckTimer.Stop();
        await Streams.ShutdownAsync();
    }
}
