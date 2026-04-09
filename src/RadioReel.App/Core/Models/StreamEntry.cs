using CommunityToolkit.Mvvm.ComponentModel;

namespace RadioReel.App.Core.Models;

public partial class StreamEntry : ObservableObject
{
    [ObservableProperty]
    private string _url = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _status = "Відключено";

    [ObservableProperty]
    private string _currentTrack = "";

    public int MaxReconnectAttempts { get; set; }

    public int ReconnectIntervalSec { get; set; } = 5;
}
