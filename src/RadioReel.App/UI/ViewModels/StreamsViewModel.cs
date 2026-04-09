using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RadioReel.App.Core.Models;

namespace RadioReel.App.UI.ViewModels;

public partial class StreamsViewModel : ObservableObject
{
    [ObservableProperty]
    private StreamEntry? _selectedStream;

    public ObservableCollection<StreamEntry> Streams { get; } = new();

    [RelayCommand]
    private void StartRecording()
    {
        // Implemented in Task 9
    }

    [RelayCommand]
    private void StopRecording()
    {
        // Implemented in Task 9
    }

    [RelayCommand]
    private void AddStream()
    {
        // Implemented in Task 10
    }

    [RelayCommand]
    private void RemoveStream()
    {
        if (SelectedStream is not null)
        {
            Streams.Remove(SelectedStream);
        }
    }
}
