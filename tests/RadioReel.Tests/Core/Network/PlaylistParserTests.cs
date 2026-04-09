using RadioReel.App.Core.Network;

namespace RadioReel.Tests.Core.Network;

public class PlaylistParserTests
{
    [Fact]
    public void ParseM3u_ReturnsFirstUrl()
    {
        var content = "#EXTM3U\n#EXTINF:-1,Station\nhttp://stream.example.com/radio\nhttp://backup.example.com/radio";
        var result = PlaylistParser.ParseM3u(content);
        Assert.Equal("http://stream.example.com/radio", result);
    }

    [Fact]
    public void ParseM3u_SkipsComments()
    {
        var content = "# comment\n\nhttp://stream.example.com/radio";
        var result = PlaylistParser.ParseM3u(content);
        Assert.Equal("http://stream.example.com/radio", result);
    }

    [Fact]
    public void ParsePls_ReturnsFile1()
    {
        var content = "[playlist]\nFile1=http://stream.example.com/radio\nTitle1=Station\nLength1=-1\nNumberOfEntries=1\nVersion=2";
        var result = PlaylistParser.ParsePls(content);
        Assert.Equal("http://stream.example.com/radio", result);
    }

    [Fact]
    public void ParseAsx_ReturnsRefHref()
    {
        var content = "<asx version=\"3.0\"><entry><ref href=\"http://stream.example.com/radio\"/></entry></asx>";
        var result = PlaylistParser.ParseAsx(content);
        Assert.Equal("http://stream.example.com/radio", result);
    }

    [Fact]
    public async Task ResolveUrl_DirectUrl_ReturnsSame()
    {
        var result = await PlaylistParser.ResolveAsync("http://stream.example.com/radio");
        Assert.Equal("http://stream.example.com/radio", result);
    }
}
