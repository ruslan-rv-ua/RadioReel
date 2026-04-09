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
    private AppSettings _cachedSettings;
    private RecordingSession? _activeSession;

    [ObservableProperty]
    private StreamEntry? _selectedStream;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveStreamCommand))]
    private bool _isRecording;

    public ObservableCollection<StreamEntry> Streams { get; } = new();

    public StreamsViewModel(ISettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        _cachedSettings = _settingsStore.Load();
        LoadStreams();
    }

    private void LoadStreams()
    {
        foreach (var stream in _cachedSettings.Streams)
            Streams.Add(stream);
    }

    public void SaveStreams()
    {
        _cachedSettings.Streams = new List<StreamEntry>(Streams);
        _settingsStore.Save(_cachedSettings);
    }

    [RelayCommand]
    private void StartRecording()
    {
        if (SelectedStream is null || IsRecording) return;

        // Capture locals so lambdas don't close over mutable fields (_activeSession, SelectedStream).
        var session = new RecordingSession(SelectedStream, _cachedSettings.Recording);
        var stream = SelectedStream;
        _activeSession = session;

        session.StateChanged += (_, _) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                stream.Status = session.Status switch
                {
                    RecordingStatus.Connecting => "Підключення...",
                    RecordingStatus.Recording => "Запис",
                    RecordingStatus.Reconnecting => "Перепідключення...",
                    RecordingStatus.Stopped => "Зупинено",
                    RecordingStatus.Error => "Помилка",
                    _ => "Відключено"
                };
                IsRecording = session.Status is
                    RecordingStatus.Recording or
                    RecordingStatus.Connecting or
                    RecordingStatus.Reconnecting;
            });
        };

        session.TrackChanged += (_, meta) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                stream.CurrentTrack = meta.StreamTitle ?? "";
                AccessibilityHelper.AnnouncePolite(
                    $"Поточний трек: {meta.StreamTitle}");
            });
        };

        session.ErrorOccurred += (_, msg) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AccessibilityHelper.AnnounceAssertive(
                    $"Помилка: {stream.Name} — {msg}");
            });
        };

        session.ReconnectAttempt += (_, attempt) =>
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                AccessibilityHelper.AnnouncePolite(
                    $"Перепідключення: {stream.Name}, спроба {attempt}");
            });
        };

        session.Start();
        IsRecording = true; // set eagerly; kept in sync by StateChanged callbacks
        AccessibilityHelper.AnnounceAssertive($"Запис розпочато: {stream.Name}");
        Logger.Information("[{Station}] Recording started by user", stream.Name);
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
                entry.Name = Uri.TryCreate(entry.Url, UriKind.Absolute, out var u)
                    ? u.Host
                    : entry.Url;

            Streams.Add(entry);
            SelectedStream = entry;
            SaveStreams();
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveStream))]
    private void RemoveStream()
    {
        if (SelectedStream is null) return;
        Streams.Remove(SelectedStream);
        SaveStreams();
    }

    private bool CanRemoveStream() => !IsRecording;

    public async Task ShutdownAsync()
    {
        if (_activeSession is not null)
        {
            await _activeSession.StopAsync();
            _activeSession.Dispose();
            _activeSession = null;
        }
        SaveStreams();
    }
}
