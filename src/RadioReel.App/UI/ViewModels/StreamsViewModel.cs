using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RadioReel.App.Core.Audio;
using RadioReel.App.Core.Models;
using RadioReel.App.Core.Storage;
using RadioReel.App.Infrastructure.Accessibility;
using Serilog;

namespace RadioReel.App.UI.ViewModels;

public partial class StreamsViewModel : ObservableObject
{
    private static readonly ILogger Logger = Log.ForContext<StreamsViewModel>();

    private readonly ISettingsStore _settingsStore;
    private RecordingSession? _activeSession;

    [ObservableProperty]
    private StreamEntry? _selectedStream;

    [ObservableProperty]
    private bool _isRecording;

    public ObservableCollection<StreamEntry> Streams { get; } = new();

    public StreamsViewModel(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        LoadStreams();
    }

    private void LoadStreams()
    {
        var settings = _settingsStore.Load();
        foreach (var stream in settings.Streams)
        {
            Streams.Add(stream);
        }
    }

    public void SaveStreams()
    {
        var settings = _settingsStore.Load();
        settings.Streams = new List<StreamEntry>(Streams);
        _settingsStore.Save(settings);
    }

    [RelayCommand]
    private void StartRecording()
    {
        if (SelectedStream is null || IsRecording) return;

        var settings = _settingsStore.Load();
        _activeSession = new RecordingSession(SelectedStream, settings.Recording);

        _activeSession.StateChanged += (_, _) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SelectedStream.Status = _activeSession.Status switch
                {
                    RecordingStatus.Connecting => "Підключення...",
                    RecordingStatus.Recording => "Запис",
                    RecordingStatus.Reconnecting => "Перепідключення...",
                    RecordingStatus.Stopped => "Зупинено",
                    RecordingStatus.Error => "Помилка",
                    _ => "Відключено"
                };
                IsRecording = _activeSession.Status == RecordingStatus.Recording
                           || _activeSession.Status == RecordingStatus.Connecting
                           || _activeSession.Status == RecordingStatus.Reconnecting;
            });
        };

        _activeSession.TrackChanged += (_, meta) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                SelectedStream.CurrentTrack = meta.StreamTitle ?? "";
                AccessibilityHelper.AnnouncePolite(
                    $"Поточний трек: {meta.StreamTitle}");
            });
        };

        _activeSession.ErrorOccurred += (_, msg) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AccessibilityHelper.AnnounceAssertive(
                    $"Помилка: {SelectedStream.Name} — {msg}");
            });
        };

        _activeSession.Start();
        AccessibilityHelper.AnnounceAssertive($"Запис розпочато: {SelectedStream.Name}");

        Logger.Information("[{Station}] Recording started by user", SelectedStream.Name);
    }

    [RelayCommand]
    private async Task StopRecordingAsync()
    {
        if (_activeSession is null) return;

        var stationName = SelectedStream?.Name ?? "Unknown";

        await _activeSession.StopAsync();
        _activeSession.Dispose();
        _activeSession = null;
        IsRecording = false;

        if (SelectedStream is not null)
        {
            SelectedStream.Status = "Відключено";
            SelectedStream.CurrentTrack = "";
        }

        AccessibilityHelper.AnnounceAssertive($"Запис зупинено: {stationName}");
        Logger.Information("[{Station}] Recording stopped by user", stationName);
    }

    [RelayCommand]
    private void AddStream()
    {
        var dialog = new UI.Views.Dialogs.AddStreamDialog
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true && dialog.Result is not null)
        {
            var entry = dialog.Result;
            if (string.IsNullOrEmpty(entry.Name))
                entry.Name = new Uri(entry.Url).Host;

            Streams.Add(entry);
            SelectedStream = entry;
            SaveStreams();
        }
    }

    [RelayCommand]
    private void RemoveStream()
    {
        if (SelectedStream is null) return;
        Streams.Remove(SelectedStream);
        SaveStreams();
    }

    public async Task ShutdownAsync()
    {
        if (_activeSession is not null)
        {
            await _activeSession.StopAsync();
            _activeSession.Dispose();
        }
        SaveStreams();
    }
}
