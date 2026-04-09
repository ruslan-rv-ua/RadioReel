using System.Diagnostics;
using System.IO;
using RadioReel.App.Core.Metadata;
using Serilog;

namespace RadioReel.App.Core.Audio;

public class TrackSplitter : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<TrackSplitter>();

    private readonly StreamRecorder _recorder;
    private readonly FileNameTemplate _trackTemplate;
    private readonly FileNameTemplate _incompleteTemplate;
    private readonly string _baseDir;
    private readonly string _extension;
    private readonly int _skipShortTracksMs;

    private readonly Stopwatch _trackStopwatch = new();
    private string _currentArtist = "";
    private string _currentTitle = "";
    private string _stationName = "";
    private int _trackNumber;
    private bool _isFirstTrack = true;

    public event EventHandler<string>? TrackCompleted;

    public TrackSplitter(
        StreamRecorder recorder,
        FileNameTemplate trackTemplate,
        FileNameTemplate incompleteTemplate,
        string baseDir,
        string stationName,
        string extension,
        int skipShortTracksMs)
    {
        _recorder = recorder;
        _trackTemplate = trackTemplate;
        _incompleteTemplate = incompleteTemplate;
        _baseDir = baseDir;
        _stationName = stationName;
        _extension = extension;
        _skipShortTracksMs = skipShortTracksMs;
    }

    public void OnAudioData(byte[] data)
    {
        _recorder.WriteData(data);
    }

    public void OnMetadataChanged(IcyMetadata metadata)
    {
        if (metadata.StreamTitle is null) return;

        // Parse "Artist - Title" format
        var parts = metadata.StreamTitle.Split(" - ", 2);
        var newArtist = parts.Length > 1 ? parts[0].Trim() : "";
        var newTitle = parts.Length > 1 ? parts[1].Trim() : parts[0].Trim();

        // Skip if same track
        if (newArtist == _currentArtist && newTitle == _currentTitle)
            return;

        // Close previous track
        FinalizePreviousTrack();

        // Start new track
        _currentArtist = newArtist;
        _currentTitle = newTitle;
        _trackNumber++;
        _trackStopwatch.Restart();

        var template = _isFirstTrack ? _incompleteTemplate : _trackTemplate;
        var filePath = template.BuildFilePath(
            _baseDir, _currentArtist, _currentTitle,
            _stationName, _trackNumber, _extension);

        _recorder.StartFile(filePath);
        _isFirstTrack = false;

        Logger.Information("[{Station}] Track changed: {Artist} - {Title}",
            _stationName, _currentArtist, _currentTitle);
    }

    private void FinalizePreviousTrack()
    {
        if (!_recorder.IsRecording) return;

        var previousPath = _recorder.CurrentFilePath;
        var elapsedMs = _trackStopwatch.ElapsedMilliseconds;

        _recorder.CloseFile();

        // Delete short tracks (ads filter). elapsedMs is always > 0 here since
        // the stopwatch starts when a track begins and IsRecording guards entry.
        if (previousPath is not null && _skipShortTracksMs > 0 && elapsedMs < _skipShortTracksMs)
        {
            try
            {
                File.Delete(previousPath);
                Logger.Information("Deleted short track ({ElapsedMs}ms < {Threshold}ms): {Path}",
                    elapsedMs, _skipShortTracksMs, previousPath);
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Failed to delete short track {Path}", previousPath);
            }
        }
        else if (previousPath is not null)
        {
            TrackCompleted?.Invoke(this, previousPath);
        }
    }

    public void FinalizeRecording()
    {
        FinalizePreviousTrack();
    }

    public void Dispose()
    {
        FinalizeRecording();
    }
}
