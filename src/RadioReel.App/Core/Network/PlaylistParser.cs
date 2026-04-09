using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;
using Serilog;

namespace RadioReel.App.Core.Network;

public static class PlaylistParser
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    /// <summary>
    /// Resolves a URL that may be a playlist (.m3u/.pls/.asx) to a direct stream URL.
    /// For direct URLs, returns as-is without making a network request.
    /// </summary>
    public static async Task<string> ResolveAsync(string url)
    {
        var uri = new Uri(url);
        var ext = Path.GetExtension(uri.AbsolutePath).ToLowerInvariant();

        if (ext is not (".m3u" or ".m3u8" or ".pls" or ".asx"))
            return url;

        try
        {
            var content = await _http.GetStringAsync(url);

            return ext switch
            {
                ".m3u" or ".m3u8" => ParseM3u(content) ?? url,
                ".pls" => ParsePls(content) ?? url,
                ".asx" => ParseAsx(content) ?? url,
                _ => url
            };
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to resolve playlist {Url}", url);
            return url;
        }
    }

    public static string? ParseM3u(string content)
    {
        return content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => !line.StartsWith('#') && line.Length > 0);
    }

    public static string? ParsePls(string content)
    {
        return content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("File1=", StringComparison.OrdinalIgnoreCase))
            .Select(line => line["File1=".Length..])
            .FirstOrDefault();
    }

    public static string? ParseAsx(string content)
    {
        try
        {
            var doc = XDocument.Parse(content);
            return doc.Descendants()
                .Where(e => e.Name.LocalName.Equals("ref", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Attribute("href")?.Value)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
