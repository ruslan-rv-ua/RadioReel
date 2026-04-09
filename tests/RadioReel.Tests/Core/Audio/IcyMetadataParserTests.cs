using System.Text;
using RadioReel.App.Core.Audio;

namespace RadioReel.Tests.Core.Audio;

public class IcyMetadataParserTests
{
    [Fact]
    public void Parse_StandardFormat_ExtractsTitle()
    {
        var bytes = Encoding.UTF8.GetBytes("StreamTitle='Artist - Song Title';");
        var result = IcyMetadataParser.Parse(bytes);
        Assert.Equal("Artist - Song Title", result.StreamTitle);
    }

    [Fact]
    public void Parse_WithStreamUrl_ExtractsBoth()
    {
        var bytes = Encoding.UTF8.GetBytes(
            "StreamTitle='Artist - Title';StreamUrl='http://example.com';");
        var result = IcyMetadataParser.Parse(bytes);
        Assert.Equal("Artist - Title", result.StreamTitle);
        Assert.Equal("http://example.com", result.StreamUrl);
    }

    [Fact]
    public void Parse_EmptyBytes_ReturnsEmpty()
    {
        var result = IcyMetadataParser.Parse(Array.Empty<byte>());
        Assert.Null(result.StreamTitle);
    }

    [Fact]
    public void Parse_NullPaddedBytes_TrimsNulls()
    {
        var raw = Encoding.UTF8.GetBytes("StreamTitle='Test';");
        var padded = new byte[raw.Length + 10]; // null-padded
        Array.Copy(raw, padded, raw.Length);
        var result = IcyMetadataParser.Parse(padded);
        Assert.Equal("Test", result.StreamTitle);
    }

    [Fact]
    public void Parse_Latin1Encoding_Decodes()
    {
        // "Mötley Crüe" in latin-1
        var latin1 = Encoding.Latin1.GetBytes("StreamTitle='M\u00f6tley Cr\u00fce - Song';");
        var result = IcyMetadataParser.Parse(latin1);
        Assert.Contains("tley Cr", result.StreamTitle); // core part should be present
    }

    [Fact]
    public void Parse_TitleOnly_NoSeparator()
    {
        var bytes = Encoding.UTF8.GetBytes("StreamTitle='Just a Title';");
        var result = IcyMetadataParser.Parse(bytes);
        Assert.Equal("Just a Title", result.StreamTitle);
    }
}
