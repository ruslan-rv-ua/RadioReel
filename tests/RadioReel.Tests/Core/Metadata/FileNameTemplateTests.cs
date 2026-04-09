using RadioReel.App.Core.Metadata;

namespace RadioReel.Tests.Core.Metadata;

public class FileNameTemplateTests
{
    [Fact]
    public void Apply_BasicSubstitution()
    {
        var template = new FileNameTemplate("%a - %t");
        var result = template.Apply("Artist", "Song", "Station", 1);
        Assert.Equal("Artist - Song", result);
    }

    [Fact]
    public void Apply_StationVariable()
    {
        var template = new FileNameTemplate("%s - %a - %t");
        var result = template.Apply("Artist", "Song", "MyStation", 1);
        Assert.Equal("MyStation - Artist - Song", result);
    }

    [Fact]
    public void Apply_SanitizesIllegalChars()
    {
        var template = new FileNameTemplate("%a - %t");
        var result = template.Apply("Art:ist", "So*ng?", "Station", 1);
        Assert.Equal("Art_ist - So_ng_", result);
    }

    [Fact]
    public void Apply_TrimsWhitespace()
    {
        var template = new FileNameTemplate("%a - %t");
        var result = template.Apply("  Artist  ", "  Song  ", "Station", 1);
        Assert.Equal("Artist - Song", result);
    }

    [Fact]
    public void Apply_DateAndTimeVariables()
    {
        var template = new FileNameTemplate("%d_%time");
        var result = template.Apply("A", "T", "S", 1);
        // Should contain date pattern YYYY-MM-DD
        Assert.Matches(@"\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}", result);
    }

    [Fact]
    public void Apply_TrackNumber()
    {
        var template = new FileNameTemplate("%n - %t");
        var result = template.Apply("A", "Song", "S", 5);
        Assert.StartsWith("5 - ", result);
    }

    [Fact]
    public void Apply_SubdirectoryFromBackslash()
    {
        var template = new FileNameTemplate(@"%s\%a - %t");
        var result = template.Apply("Artist", "Song", "Station", 1);
        Assert.Equal(@"Station\Artist - Song", result);
    }

    [Fact]
    public void GetUniqueFilePath_NoConflict_ReturnsOriginal()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rr_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var path = FileNameTemplate.GetUniqueFilePath(Path.Combine(dir, "test.mp3"));
        Assert.EndsWith("test.mp3", path);
    }

    [Fact]
    public void GetUniqueFilePath_Conflict_AppendsSuffix()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"rr_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var original = Path.Combine(dir, "test.mp3");
        File.WriteAllText(original, "dummy");

        var path = FileNameTemplate.GetUniqueFilePath(original);
        Assert.EndsWith("test_2.mp3", path);
    }
}
