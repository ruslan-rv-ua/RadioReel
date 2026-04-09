using System.IO;
using RadioReel.App.Core.Metadata;
using RadioReel.App.Core.Models;
using RadioReel.App.Core.Network;
using RadioReel.App.Core.Storage;
using Serilog;

namespace RadioReel.App.Core.Audio;

public sealed class RecordingSession : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<RecordingSession>();
    private const int MaxBackoffSec = 300;

    private readonly StreamEntry _stream;
    private readonly RecordingSettings _recordingSettings;

    // Written from RunAsync task, read from StopAsync/Dispose caller thread.
    // volatile ensures visibility across threads without a full lock.
    private volatile IIcyStreamClient? _client;
    private volatile StreamRecorder? _recorder;
    private volatile TrackSplitter? _splitter;
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
    public event EventHandler<int>? ReconnectAttempt;

    public RecordingSession(StreamEntry stream, RecordingSettings recordingSettings)
    {
        _stream = stream;
        _recordingSettings = recordingSettings;
    }

    public void Start()
    {
        if (Status is RecordingStatus.Recording or RecordingStatus.Connecting or RecordingStatus.Reconnecting)
            return;

        _cts = new CancellationTokenSource();
        _sessionTask = RunAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        var cts = _cts;
        if (cts is null) return;

        await cts.CancelAsync();

        var sessionTask = _sessionTask;
        if (sessionTask is not null)
        {
            try { await sessionTask; } catch (OperationCanceledException) { }
        }

        // RunAsync has exited — finalize and dispose remaining resources
        _splitter?.Dispose();   // calls FinalizeRecording internally
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
            IIcyStreamClient? client = null;
            StreamRecorder? recorder = null;
            TrackSplitter? splitter = null;

            try
            {
                SetStatus(RecordingStatus.Connecting);

                var url = await PlaylistParser.ResolveAsync(_stream.Url);

                client = new IcyStreamClient();
                recorder = new StreamRecorder();

                await client.ConnectAsync(url, ct);
                ct.ThrowIfCancellationRequested();

                var extension = client.ContentType?.ToLowerInvariant() switch
                {
                    "audio/aacp" or "audio/aac" => ".aac",
                    _ => ".mp3"
                };

                var stationName = _stream.Name.Length > 0 ? _stream.Name : (client.StationName ?? "Unknown");

                var baseDir = Path.IsPathRooted(_recordingSettings.DefaultOutputDir)
                    ? _recordingSettings.DefaultOutputDir
                    : Path.Combine(AppPaths.BaseDir, _recordingSettings.DefaultOutputDir);

                splitter = new TrackSplitter(
                    recorder,
                    new FileNameTemplate(_recordingSettings.FileNameTemplate),
                    new FileNameTemplate(_recordingSettings.IncompleteFileNameTemplate),
                    baseDir,
                    stationName,
                    extension,
                    _recordingSettings.SkipShortTracksMs);

                splitter.TrackCompleted += (_, _) => RecordedTracksCount++;

                // Publish to fields so StopAsync/Dispose can reach them
                _client = client;
                _recorder = recorder;
                _splitter = splitter;

                // Wire events
                client.AudioDataReceived += (_, data) => splitter.OnAudioData(data);
                client.MetadataChanged += (_, meta) =>
                {
                    splitter.OnMetadataChanged(meta);
                    CurrentTrackTitle = meta.StreamTitle;
                    TrackChanged?.Invoke(this, meta);
                    StateChanged?.Invoke(this, EventArgs.Empty);
                };

                // Single Error handler: forward to UI and signal disconnect
                var disconnectTcs = new TaskCompletionSource();
                client.Error += (_, msg) =>
                {
                    ErrorOccurred?.Invoke(this, msg);
                    disconnectTcs.TrySetResult();
                };
                client.Disconnected += (_, reason) =>
                {
                    Logger.Information("[{Station}] Disconnected: {Reason}", stationName, reason);
                    disconnectTcs.TrySetResult();
                };

                ConnectedAt = DateTime.Now;
                attempt = 0;
                SetStatus(RecordingStatus.Recording);

                // Wait until stream ends or user cancels.
                // Use TrySetResult on cancellation (not TrySetCanceled) to avoid
                // OperationCanceledException racing with TrySetResult from Disconnected.
                using var reg = ct.Register(() => disconnectTcs.TrySetResult());
                await disconnectTcs.Task;

                // Now check whether we exited due to cancellation or genuine disconnect
                ct.ThrowIfCancellationRequested();

                // Genuine disconnect — clean up before reconnecting
                Logger.Information("[{Station}] Cleaning up after disconnect", stationName);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "[{Station}] Connection failed (attempt {Attempt})",
                    _stream.Name, attempt + 1);
                attempt++;

                if (maxAttempts > 0 && attempt >= maxAttempts)
                {
                    SetStatus(RecordingStatus.Error);
                    ErrorOccurred?.Invoke(this, $"Max reconnect attempts ({maxAttempts}) reached");
                    break;
                }

                var exponent = Math.Min(attempt - 1, 10);
                var delaySec = Math.Min(baseInterval * (1 << exponent), MaxBackoffSec);
                SetStatus(RecordingStatus.Reconnecting);
                ReconnectAttempt?.Invoke(this, attempt);
                ErrorOccurred?.Invoke(this, $"Reconnecting in {delaySec}s (attempt {attempt})");

                try { await Task.Delay(TimeSpan.FromSeconds(delaySec), ct); }
                catch (OperationCanceledException) { break; }
            }
            finally
            {
                // Always dispose this iteration's objects before the next iteration or exit.
                // Null the shared fields first so StopAsync/Dispose don't double-dispose.
                _client = null;
                _recorder = null;
                _splitter = null;

                splitter?.Dispose();   // calls FinalizeRecording
                recorder?.Dispose();
                client?.Dispose();
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
        // Block until RunAsync exits so we don't race with its finally block.
        try { _sessionTask?.GetAwaiter().GetResult(); } catch (OperationCanceledException) { }
        _cts?.Dispose();
        // Resources are cleaned up by RunAsync's finally block; these are no-ops if already disposed.
        _splitter?.Dispose();
        _recorder?.Dispose();
        _client?.Dispose();
    }
}
