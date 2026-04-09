using System.Text;
using System.Text.RegularExpressions;
using RadioReel.App.Core.Metadata;

namespace RadioReel.App.Core.Audio;

public static partial class IcyMetadataParser
{
    [GeneratedRegex(@"StreamTitle='(.*?)';", RegexOptions.Singleline)]
    private static partial Regex StreamTitleRegex();

    [GeneratedRegex(@"StreamUrl='(.*?)';", RegexOptions.Singleline)]
    private static partial Regex StreamUrlRegex();

    public static IcyMetadata Parse(byte[] data)
    {
        if (data.Length == 0)
            return new IcyMetadata(null, null);

        var text = DecodeMetadata(data);

        var titleMatch = StreamTitleRegex().Match(text);
        var urlMatch = StreamUrlRegex().Match(text);

        return new IcyMetadata(
            titleMatch.Success ? titleMatch.Groups[1].Value : null,
            urlMatch.Success ? urlMatch.Groups[1].Value : null);
    }

    private static string DecodeMetadata(byte[] data)
    {
        // Trim null padding
        var length = Array.IndexOf(data, (byte)0);
        if (length < 0) length = data.Length;
        if (length == 0) return string.Empty;

        var trimmed = data.AsSpan(0, length);

        // Try strict UTF-8 first (throws on invalid bytes)
        try
        {
            var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            return strictUtf8.GetString(trimmed);
        }
        catch
        {
            // Fallback to Latin-1
            return Encoding.Latin1.GetString(trimmed);
        }
    }
}
