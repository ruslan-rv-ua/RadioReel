using System.IO;
using System.Text.RegularExpressions;

namespace RadioReel.App.Core.Metadata;

public partial class FileNameTemplate
{
    private static readonly char[] IllegalChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    private readonly string _template;

    public FileNameTemplate(string template)
    {
        _template = template;
    }

    /// <summary>
    /// Apply template variables and return a relative path (may contain subdirectories via \).
    /// </summary>
    public string Apply(string artist, string title, string station, int trackNumber)
    {
        var now = DateTime.Now;
        var result = _template
            .Replace("%time", now.ToString("HH-mm-ss"))
            .Replace("%d", now.ToString("yyyy-MM-dd"))
            .Replace("%a", Sanitize(artist.Trim()))
            .Replace("%t", Sanitize(title.Trim()))
            .Replace("%s", Sanitize(station.Trim()))
            .Replace("%n", trackNumber.ToString());

        return result;
    }

    /// <summary>
    /// Build a full file path, creating subdirectories as needed, with collision avoidance.
    /// </summary>
    public string BuildFilePath(string baseDir, string artist, string title,
        string station, int trackNumber, string extension)
    {
        var relative = Apply(artist, title, station, trackNumber);
        var fullPath = Path.Combine(baseDir, relative + extension);

        var dir = Path.GetDirectoryName(fullPath);
        if (dir is not null)
            Directory.CreateDirectory(dir);

        return GetUniqueFilePath(fullPath);
    }

    public static string GetUniqueFilePath(string path)
    {
        if (!File.Exists(path))
            return path;

        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);

        var counter = 2;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{name}_{counter}{ext}");
            counter++;
        } while (File.Exists(candidate));

        return candidate;
    }

    private static string Sanitize(string input)
    {
        var result = input;
        foreach (var c in IllegalChars)
        {
            result = result.Replace(c, '_');
        }
        return result.Trim();
    }
}
