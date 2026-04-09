using RadioReel.App.Core.Storage;

namespace RadioReel.Tests.Core.Storage;

public class SettingsStoreTests
{
    private readonly string _tempDir;

    public SettingsStoreTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"RadioReelTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public void Load_NoFile_ReturnsDefaults()
    {
        var path = Path.Combine(_tempDir, "missing.json");
        var store = new SettingsStore(path);

        var settings = store.Load();

        Assert.Equal(1.0, settings.General.LowDiskSpaceWarningGb);
        Assert.Equal("recordings", settings.Recording.DefaultOutputDir);
        Assert.Equal(30000, settings.Recording.SkipShortTracksMs);
        Assert.Empty(settings.Streams);
    }

    [Fact]
    public void SaveAndLoad_RoundTrip()
    {
        var path = Path.Combine(_tempDir, "settings.json");
        var store = new SettingsStore(path);

        var settings = store.Load();
        settings.Streams.Add(new() { Url = "http://test.com/stream", Name = "Test" });
        store.Save(settings);

        var loaded = store.Load();
        Assert.Single(loaded.Streams);
        Assert.Equal("http://test.com/stream", loaded.Streams[0].Url);
        Assert.Equal("Test", loaded.Streams[0].Name);
    }

    [Fact]
    public void Load_CorruptedFile_ReturnsDefaults()
    {
        var path = Path.Combine(_tempDir, "corrupt.json");
        File.WriteAllText(path, "not json {{{");
        var store = new SettingsStore(path);

        var settings = store.Load();

        Assert.Equal(1.0, settings.General.LowDiskSpaceWarningGb);
    }
}
