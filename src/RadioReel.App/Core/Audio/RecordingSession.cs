using System.IO;
using RadioReel.App.Core.Metadata;
using RadioReel.App.Core.Models;
using RadioReel.App.Core.Network;
using RadioReel.App.Core.Storage;
using Serilog;

namespace RadioReel.App.Core.Audio;

public class RecordingSession : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<RecordingSession>();
    private const int MaxBackoffSec = 300;

    private readonly StreamEntry _stream;
    private readonly RecordingSettings _recordingSettings;

    private IcyStreamClient? _client;
    private StreamRecorder? _recorder;
    private TrackSplitter? _splitter;
    private CancellationTokenSource? _cts;
    private Task? _sessionTask;

    // Observable state
    public RecordingStatus Status { get; private set; } = RecordingStatus.Idle;
    public string? CurrentTrackTitle { get; private set; }
    public int RecordedTracksCount { get; private set; }
    public long BytesRecorded => _recorder?.BytesWritten ?? 0;
    public DateTime? ConnectedAt { get; private set; }

    // Events for UI binding
    public event EventHandler? StateChanged;
    public event EventHandler<IcyMetadata>? TrackChanged;
    public event EventHandler<string>? ErrorOccurred;

    public RecordingSession(StreamEntry stream, RecordingSettings recordingSettings)
    {
        _stream = stream;
        _recordingSettings = recordingSettings;
    }

    public void Start()
    {
        if (Status == RecordingStatus.Recording || Status == RecordingStatus.Connecting)
            return;

        _cts = new CancellationTokenSource();
        _sessionTask = RunAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        if (_cts is null) return;

        await _cts.CancelAsync();

        if (_sessionTask is not null)
        {
            try { await _sessionTask; } catch (OperationCanceledException) { }
        }

        _splitter?.FinalizeRecording();
        _recorder?.Dispose();
        _client?.Dispose();

        SetStatus(RecordingStatus.Stopped);

        Logger.Information("[{Station}] Recording stopped", _stream.Name);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var attempt = 0;
        var maxAttempts = _stream.MaxReconnectAttempts > 0
            ? _stream.MaxReconnectAttempts
            : _recordingSettings.MaxReconnectAttempts;
        var baseInterval = _stream.ReconnectIntervalSec > 0
            ? _stream.ReconnectIntervalSec
            : _recordingSettings.ReconnectIntervalSec;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                SetStatus(RecordingStatus.Connecting);

                var url = await PlaylistParser.ResolveAsync(_stream.Url);

                _client = new IcyStreamClient();
                _recorder = new StreamRecorder();

                var extension = ".mp3"; // default, updated from content-type after connect

                await _client.ConnectAsync(url, ct);

                // Determine extension from content type
                extension = _client.ContentType?.ToLowerInvariant() switch
                {
                    "audio/aacp" or "audio/aac" => ".aac",
                    _ => ".mp3"
                };

                var stationName = _stream.Name.Length > 0 ? _stream.Name : (_client.StationName ?? "Unknown");

                var baseDir = Path.IsPathRooted(_recordingSettings.DefaultOutputDir)
                    ? _recordingSettings.DefaultOutputDir
                    : Path.Combine(AppPaths.BaseDir, _recordingSettings.DefaultOutputDir);

                _splitter = new TrackSplitter(
                    _recorder,
                    new FileNameTemplate(_recordingSettings.FileNameTemplate),
                    new FileNameTemplate(_recordingSettings.IncompleteFileNameTemplate),
                    baseDir,
                    stationName,
                    extension,
                    _recordingSettings.SkipShortTracksMs);

                _splitter.TrackCompleted += (_, path) => RecordedTracksCount++;

                // Wire events
                _client.AudioDataReceived += (_, data) => _splitter.OnAudioData(data);
                _client.MetadataChanged += (_, meta) =>
                {
                    _splitter.OnMetadataChanged(meta);
                    CurrentTrackTitle = meta.StreamTitle;
                    TrackChanged?.Invoke(this, meta);
                    StateChanged?.Invoke(this, EventArgs.Empty);
                };
                _client.Error += (_, msg) =>
                {
                    ErrorOccurred?.Invoke(this, msg);
                };

                ConnectedAt = DateTime.Now;
                attempt = 0;
                SetStatus(RecordingStatus.Recording);

                // Wait for disconnect or cancellation
                var disconnectTcs = new TaskCompletionSource();

                _client.Disconnected += (_, reason) =>
                {
                    Logger.Information("[{Station}] Disconnected: {Reason}", stationName, reason);
                    disconnectTcs.TrySetResult();
                };

                _client.Error += (_, _) =>
                {
                    disconnectTcs.TrySetResult();
                };

                // Wait until stream disconnects or user cancels
                using var reg = ct.Register(() => disconnectTcs.TrySetCanceled());
                await disconnectTcs.Task;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "[{Station}] Connection failed (attempt {Attempt})",
                    _stream.Name, attempt + 1);

                _splitter?.FinalizeRecording();
                _recorder?.Dispose();
                _client?.Dispose();

                attempt++;

                if (maxAttempts > 0 && attempt >= maxAttempts)
                {
                    SetStatus(RecordingStatus.Error);
                    ErrorOccurred?.Invoke(this, $"Max reconnect attempts ({maxAttempts}) reached");
                    break;
                }

                // Exponential backoff, capped at MaxBackoffSec
                var delaySec = Math.Min(baseInterval * (1 << Math.Min(attempt - 1, 10)), MaxBackoffSec);
                SetStatus(RecordingStatus.Reconnecting);
                ErrorOccurred?.Invoke(this, $"Перепідключення через {delaySec}с (спроба {attempt})");

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySec), ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private void SetStatus(RecordingStatus status)
    {
        Status = status;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _splitter?.Dispose();
        _recorder?.Dispose();
        _client?.Dispose();
    }
}
