using System.IO;
using System.Threading;
using Serilog;

namespace RadioReel.App.Core.Audio;

public sealed class StreamRecorder : IDisposable
{
    private static readonly ILogger Logger = Log.ForContext<StreamRecorder>();
    private readonly object _lock = new();

    private FileStream? _fileStream;
    private long _bytesWritten;

    public string? CurrentFilePath { get; private set; }
    public long BytesWritten => Interlocked.Read(ref _bytesWritten);
    public bool IsRecording { get { lock (_lock) { return _fileStream is not null; } } }

    public void StartFile(string filePath)
    {
        lock (_lock)
        {
            CloseFileLocked();

            var dir = Path.GetDirectoryName(filePath);
            if (dir is not null)
                Directory.CreateDirectory(dir);

            _fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            CurrentFilePath = filePath;
            Interlocked.Exchange(ref _bytesWritten, 0);

            Logger.Information("Recording to {FilePath}", filePath);
        }
    }

    public void WriteData(byte[] data)
    {
        lock (_lock)
        {
            if (_fileStream is null) return;
            _fileStream.Write(data, 0, data.Length);
            Interlocked.Add(ref _bytesWritten, data.Length);
        }
    }

    public void CloseFile()
    {
        lock (_lock)
        {
            CloseFileLocked();
        }
    }

    private void CloseFileLocked()
    {
        if (_fileStream is not null)
        {
            _fileStream.Flush();
            _fileStream.Dispose();
            _fileStream = null;

            Logger.Information("Closed recording file {FilePath} ({Bytes} bytes)",
                CurrentFilePath, Interlocked.Read(ref _bytesWritten));
        }
    }

    public void Dispose()
    {
        CloseFile();
    }
}
