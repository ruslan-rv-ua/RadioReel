using System.IO;
using RadioReel.App.Core.Audio;
using RadioReel.App.Core.Metadata;

namespace RadioReel.Tests.Core.Audio;

/// <summary>
/// Integration-style unit tests for TrackSplitter using real temp files (no mocks).
/// Each test creates its own temp directory and cleans up in Dispose().
/// </summary>
public class TrackSplitterTests : IDisposable
{
    // One temp root per test class instance — each [Fact] creates a sub-directory inside.
    private readonly string _tempRoot;

    public TrackSplitterTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "RadioReel_TrackSplitterTests_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private string MakeDir(string name)
    {
        var dir = Path.Combine(_tempRoot, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static (TrackSplitter splitter, StreamRecorder recorder) MakeSplitter(
        string baseDir,
        string trackTemplateStr = "track_%n",
        string incompleteTemplateStr = "incomplete_%n",
        string stationName = "TestStation",
        string extension = ".mp3",
        int skipShortTracksMs = 0)
    {
        var recorder = new StreamRecorder();
        var splitter = new TrackSplitter(
            recorder,
            new FileNameTemplate(trackTemplateStr),
            new FileNameTemplate(incompleteTemplateStr),
            baseDir,
            stationName,
            extension,
            skipShortTracksMs);
        return (splitter, recorder);
    }

    // -----------------------------------------------------------------------
    // 1. First metadata → uses _incompleteTemplate
    // -----------------------------------------------------------------------
    [Fact]
    public void FirstMetadata_UsesIncompleteTemplate()
    {
        var dir = MakeDir("test_first_incomplete");
        var (splitter, recorder) = MakeSplitter(dir,
            trackTemplateStr: "track_%n",
            incompleteTemplateStr: "incomplete_%n");

        using (splitter)
        using (recorder)
        {
            splitter.OnMetadataChanged(new IcyMetadata("Artist - Title", null));

            var path = recorder.CurrentFilePath!;
            var fileName = Path.GetFileNameWithoutExtension(path);
            Assert.StartsWith("incomplete_", fileName,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    // -----------------------------------------------------------------------
    // 2. Second metadata → uses _trackTemplate
    // -----------------------------------------------------------------------
    [Fact]
    public void SecondMetadata_UsesTrackTemplate()
    {
        var dir = MakeDir("test_second_track");
        var (splitter, recorder) = MakeSplitter(dir,
            trackTemplateStr: "track_%n",
            incompleteTemplateStr: "incomplete_%n");

        using (splitter)
        using (recorder)
        {
            splitter.OnMetadataChanged(new IcyMetadata("First Artist - First Title", null));
            splitter.OnMetadataChanged(new IcyMetadata("Second Artist - Second Title", null));

            var path = recorder.CurrentFilePath!;
            var fileName = Path.GetFileNameWithoutExtension(path);
            Assert.StartsWith("track_", fileName, StringComparison.OrdinalIgnoreCase);
        }
    }

    // -----------------------------------------------------------------------
    // 3. Same metadata twice → no new track (file path unchanged)
    // -----------------------------------------------------------------------
    [Fact]
    public void SameMetadataTwice_NoNewTrack()
    {
        var dir = MakeDir("test_same_metadata");
        var (splitter, recorder) = MakeSplitter(dir);

        using (splitter)
        using (recorder)
        {
            splitter.OnMetadataChanged(new IcyMetadata("Artist - Title", null));
            var firstPath = recorder.CurrentFilePath;

            splitter.OnMetadataChanged(new IcyMetadata("Artist - Title", null));
            var secondPath = recorder.CurrentFilePath;

            Assert.Equal(firstPath, secondPath);
        }
    }

    // -----------------------------------------------------------------------
    // 4. Null StreamTitle → ignored (no crash, no recording started)
    // -----------------------------------------------------------------------
    [Fact]
    public void NullStreamTitle_IsIgnored()
    {
        var dir = MakeDir("test_null_title");
        var (splitter, recorder) = MakeSplitter(dir);

        using (splitter)
        using (recorder)
        {
            // Should not throw
            splitter.OnMetadataChanged(new IcyMetadata(null, null));
            Assert.False(recorder.IsRecording);
        }
    }

    // -----------------------------------------------------------------------
    // 5. Short track filter → file is deleted
    // -----------------------------------------------------------------------
    [Fact]
    public void ShortTrack_IsDeleted_WhenSkipThresholdIsHigh()
    {
        var dir = MakeDir("test_short_delete");
        // int.MaxValue means every track is "too short" (elapsed always < MaxValue)
        var (splitter, recorder) = MakeSplitter(dir, skipShortTracksMs: int.MaxValue);

        string? firstFilePath = null;

        using (splitter)
        using (recorder)
        {
            splitter.OnMetadataChanged(new IcyMetadata("Artist A - Song A", null));
            firstFilePath = recorder.CurrentFilePath;

            // Trigger finalization by sending a different track
            splitter.OnMetadataChanged(new IcyMetadata("Artist B - Song B", null));
        }

        // The first file was created but then deleted by the short-track filter
        Assert.NotNull(firstFilePath);
        Assert.False(File.Exists(firstFilePath), "Short track file should have been deleted.");
    }

    // -----------------------------------------------------------------------
    // 6. skipShortTracksMs = 0 → no filter, file NOT deleted
    // -----------------------------------------------------------------------
    [Fact]
    public void ShortTrack_NotDeleted_WhenSkipThresholdIsZero()
    {
        var dir = MakeDir("test_no_filter");
        var (splitter, recorder) = MakeSplitter(dir, skipShortTracksMs: 0);

        string? firstFilePath = null;

        using (splitter)
        using (recorder)
        {
            splitter.OnMetadataChanged(new IcyMetadata("Artist A - Song A", null));
            firstFilePath = recorder.CurrentFilePath;

            splitter.OnMetadataChanged(new IcyMetadata("Artist B - Song B", null));
        }

        Assert.NotNull(firstFilePath);
        Assert.True(File.Exists(firstFilePath), "File should NOT be deleted when skipShortTracksMs = 0.");
    }

    // -----------------------------------------------------------------------
    // 7. TrackCompleted fires for non-short tracks
    // -----------------------------------------------------------------------
    [Fact]
    public void TrackCompleted_FiresForNormalDurationTrack()
    {
        var dir = MakeDir("test_track_completed_fires");
        var (splitter, recorder) = MakeSplitter(dir, skipShortTracksMs: 0);

        var completedPaths = new List<string>();
        splitter.TrackCompleted += (_, path) => completedPaths.Add(path);

        using (splitter)
        using (recorder)
        {
            splitter.OnMetadataChanged(new IcyMetadata("Artist A - Song A", null));
            splitter.OnMetadataChanged(new IcyMetadata("Artist B - Song B", null));
        }

        Assert.Single(completedPaths);
    }

    // -----------------------------------------------------------------------
    // 8. TrackCompleted does NOT fire for deleted (short) tracks
    // -----------------------------------------------------------------------
    [Fact]
    public void TrackCompleted_DoesNotFire_ForDeletedShortTrack()
    {
        var dir = MakeDir("test_no_event_for_short");
        var (splitter, recorder) = MakeSplitter(dir, skipShortTracksMs: int.MaxValue);

        var completedPaths = new List<string>();
        splitter.TrackCompleted += (_, path) => completedPaths.Add(path);

        using (splitter)
        using (recorder)
        {
            splitter.OnMetadataChanged(new IcyMetadata("Artist A - Song A", null));
            splitter.OnMetadataChanged(new IcyMetadata("Artist B - Song B", null));
        }

        Assert.Empty(completedPaths);
    }

    // -----------------------------------------------------------------------
    // 9. Dispose() finalizes the last track
    // -----------------------------------------------------------------------
    [Fact]
    public void Dispose_FinalizesLastTrack()
    {
        var dir = MakeDir("test_dispose_finalizes");
        var (splitter, recorder) = MakeSplitter(dir, skipShortTracksMs: 0);

        var completedPaths = new List<string>();
        splitter.TrackCompleted += (_, path) => completedPaths.Add(path);

        string? lastFilePath;
        using (recorder)
        {
            splitter.OnMetadataChanged(new IcyMetadata("Artist - Song", null));
            lastFilePath = recorder.CurrentFilePath;
            splitter.Dispose(); // explicitly dispose before recorder
        }

        Assert.NotNull(lastFilePath);
        Assert.Single(completedPaths);
        Assert.Equal(lastFilePath, completedPaths[0]);
    }

    // -----------------------------------------------------------------------
    // 10. Track number increments with each distinct metadata change
    // -----------------------------------------------------------------------
    [Fact]
    public void TrackNumber_IncrementsWithEachDistinctMetadata()
    {
        var dir = MakeDir("test_track_number");
        // Use %n in template so we can verify the number in the filename
        var (splitter, recorder) = MakeSplitter(dir,
            trackTemplateStr: "track_%n",
            incompleteTemplateStr: "incomplete_%n");

        var filePaths = new List<string>();
        splitter.TrackCompleted += (_, path) => filePaths.Add(path);

        using (recorder)
        using (splitter)
        {
            splitter.OnMetadataChanged(new IcyMetadata("A - 1", null));
            splitter.OnMetadataChanged(new IcyMetadata("A - 2", null));
            splitter.OnMetadataChanged(new IcyMetadata("A - 3", null));
            // FinalizeRecording called in Dispose (splitter disposes before recorder)
        }

        // Tracks 1 and 2 are finalized when track 3 starts; track 3 is finalized on Dispose.
        // Track 1 uses incompleteTemplate, 2 and 3 use trackTemplate.
        // filePaths has 3 entries (skipShortTracksMs=0, no filtering).
        Assert.Equal(3, filePaths.Count);

        // Verify that track 2 has "track_2" and track 3 has "track_3" in their paths.
        Assert.Contains(filePaths, p => Path.GetFileNameWithoutExtension(p).Contains("2"));
        Assert.Contains(filePaths, p => Path.GetFileNameWithoutExtension(p).Contains("3"));
    }

    // -----------------------------------------------------------------------
    // 11. "Artist - Title" parsing — dash separator splits correctly
    // -----------------------------------------------------------------------
    [Fact]
    public void Parsing_ArtistDashTitle_SplitsCorrectly()
    {
        var dir = MakeDir("test_parse_artist_title");
        // Use %a and %t in template to capture parsed values in filename
        var (splitter, recorder) = MakeSplitter(dir,
            trackTemplateStr: "%a__%t",
            incompleteTemplateStr: "%a__%t");

        using (splitter)
        using (recorder)
        {
            splitter.OnMetadataChanged(new IcyMetadata("Dire Straits - Sultans of Swing", null));

            var path = recorder.CurrentFilePath!;
            var fileName = Path.GetFileNameWithoutExtension(path);

            // After sanitization the template %a__%t should yield "Dire Straits__Sultans of Swing"
            Assert.Contains("Dire Straits", fileName);
            Assert.Contains("Sultans of Swing", fileName);
        }
    }

    // -----------------------------------------------------------------------
    // 12. No dash → empty artist, full string as title
    // -----------------------------------------------------------------------
    [Fact]
    public void Parsing_NoDashInTitle_UsesWholeStringAsTitle()
    {
        var dir = MakeDir("test_parse_no_dash");
        // Template uses both %a and %t; with empty artist, separator __ will appear at start
        var (splitter, recorder) = MakeSplitter(dir,
            trackTemplateStr: "%a__%t",
            incompleteTemplateStr: "%a__%t");

        using (splitter)
        using (recorder)
        {
            splitter.OnMetadataChanged(new IcyMetadata("RadioHead", null));

            var path = recorder.CurrentFilePath!;
            var fileName = Path.GetFileNameWithoutExtension(path);

            // Artist is empty → template starts with __RadioHead
            Assert.StartsWith("__RadioHead", fileName, StringComparison.Ordinal);
        }
    }
}
