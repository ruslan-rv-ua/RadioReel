using RadioReel.App.Core.Metadata;

namespace RadioReel.App.Core.Audio;

public interface IIcyStreamClient : IDisposable
{
    int MetaInt { get; }
    string? StationName { get; }
    string? ContentType { get; }
    bool IsConnected { get; }

    event EventHandler<byte[]>? AudioDataReceived;
    event EventHandler<IcyMetadata>? MetadataChanged;
    event EventHandler<string>? Disconnected;
    event EventHandler<string>? Error;

    Task ConnectAsync(string url, CancellationToken cancellationToken = default);
    Task StopAsync();
}
