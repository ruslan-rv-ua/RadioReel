using System.IO;
using Serilog;

namespace RadioReel.App.Core.Audio;

public class StreamRecorder : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<StreamRecorder>();

    private FileStream? _fileStream;
    private long _bytesWritten;

    public string? CurrentFilePath { get; private set; }
    public long BytesWritten => _bytesWritten;
    public bool IsRecording => _fileStream is not null;

    public void StartFile(string filePath)
    {
        CloseFile();

        var dir = Path.GetDirectoryName(filePath);
        if (dir is not null)
            Directory.CreateDirectory(dir);

        _fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
        CurrentFilePath = filePath;
        _bytesWritten = 0;

        Logger.Information("Recording to {FilePath}", filePath);
    }

    public void WriteData(byte[] data)
    {
        if (_fileStream is null) return;
        _fileStream.Write(data, 0, data.Length);
        _bytesWritten += data.Length;
    }

    public void CloseFile()
    {
        if (_fileStream is not null)
        {
            _fileStream.Flush();
            _fileStream.Dispose();
            _fileStream = null;

            Logger.Information("Closed recording file {FilePath} ({Bytes} bytes)",
                CurrentFilePath, _bytesWritten);
        }
    }

    public void Dispose()
    {
        CloseFile();
    }
}
