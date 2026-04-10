using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using RadioReel.App.Core.Metadata;
using Serilog;

namespace RadioReel.App.Core.Audio;

public sealed class IcyStreamClient : IIcyStreamClient
{
    private static readonly ILogger Logger = Log.ForContext<IcyStreamClient>();

    private TcpClient? _tcpClient;
    private Stream? _stream;
    private CancellationTokenSource? _cts;
    private Task? _readTask;
    private bool _disposed;

    // ICY headers parsed on connect
    public int MetaInt { get; private set; }
    public string? StationName { get; private set; }
    public string? ContentType { get; private set; }
    public bool IsConnected { get; private set; }

    // Events
    public event EventHandler<byte[]>? AudioDataReceived;
    public event EventHandler<IcyMetadata>? MetadataChanged;
    public event EventHandler<string>? Disconnected;
    public event EventHandler<string>? Error;

    public async Task ConnectAsync(string url, CancellationToken cancellationToken = default)
    {
        if (_readTask != null)
            throw new InvalidOperationException("Already connected. Create a new instance to reconnect.");

        var uri = new Uri(url);
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : (uri.Scheme == "https" ? 443 : 80);
        var path = uri.PathAndQuery;
        var useSsl = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

        var tcpClient = new TcpClient();
        try
        {
            var connectTask = tcpClient.ConnectAsync(host, port, cancellationToken).AsTask();
            var delayTask = Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            if (await Task.WhenAny(connectTask, delayTask) != connectTask)
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new TimeoutException($"Connection to {host}:{port} timed out.");
            }
            await connectTask; // propagate any connect exception

            Stream networkStream = tcpClient.GetStream();

            if (useSsl)
            {
                var sslStream = new SslStream(networkStream);
                try
                {
                    await sslStream.AuthenticateAsClientAsync(host);
                }
                catch
                {
                    await sslStream.DisposeAsync();
                    throw;
                }
                networkStream = sslStream;
            }

            // Send ICY request
            var request = $"GET {path} HTTP/1.0\r\n" +
                          $"Host: {host}\r\n" +
                          "Icy-MetaData: 1\r\n" +
                          "User-Agent: RadioReel/1.0\r\n" +
                          "Connection: close\r\n" +
                          "\r\n";

            var requestBytes = Encoding.ASCII.GetBytes(request);
            await networkStream.WriteAsync(requestBytes, cancellationToken);

            // Assign only after all setup succeeded
            _tcpClient = tcpClient;
            _stream = networkStream;
            tcpClient = null; // ownership transferred

            await ParseHeadersAsync(cancellationToken);

            Logger.Information("[{Station}] Connected. MetaInt={MetaInt}, ContentType={ContentType}",
                StationName ?? host, MetaInt, ContentType);

            IsConnected = true;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _readTask = Task.Run(() => ReadLoopAsync(_cts.Token), _cts.Token);
        }
        catch
        {
            tcpClient?.Dispose();
            _stream?.Dispose();
            _stream = null;
            _tcpClient?.Dispose();
            _tcpClient = null;
            throw;
        }
    }

    private async Task ParseHeadersAsync(CancellationToken ct)
    {
        var buffer = new byte[1];
        var lineBuffer = new StringBuilder();
        var isFirstLine = true;

        while (true)
        {
            var read = await _stream!.ReadAsync(buffer, ct);
            if (read == 0) throw new IOException("Connection closed during header read");

            var c = (char)buffer[0];
            if (c == '\n')
            {
                var line = lineBuffer.ToString().TrimEnd('\r');
                if (line.Length == 0) break; // End of headers

                if (isFirstLine)
                {
                    // Validate: "ICY 200 OK" or "HTTP/1.x 200 OK"
                    var parts = line.Split(' ');
                    if (parts.Length < 2 || parts[1] != "200")
                        throw new InvalidOperationException($"Server returned non-200 status: {line}");
                    isFirstLine = false;
                }
                else
                {
                    var colonIdx = line.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        var key = line[..colonIdx].Trim().ToLowerInvariant();
                        var value = line[(colonIdx + 1)..].Trim();

                        switch (key)
                        {
                            case "icy-metaint":
                                if (int.TryParse(value, out var metaInt))
                                    MetaInt = metaInt;
                                else
                                    Logger.Warning("Malformed icy-metaint value: {Value}. Metadata extraction disabled.", value);
                                break;
                            case "icy-name":
                                StationName = value;
                                break;
                            case "content-type":
                                ContentType = value;
                                break;
                        }
                    }
                }

                lineBuffer.Clear();
            }
            else
            {
                lineBuffer.Append(c);
            }
        }

        if (MetaInt <= 0)
            Logger.Warning("No icy-metaint header received. Metadata extraction disabled.");
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        try
        {
            var audioBuffer = new byte[MetaInt > 0 ? MetaInt : 16384];
            var metaLenBuf = new byte[1];

            while (!ct.IsCancellationRequested)
            {
                if (MetaInt > 0)
                {
                    // Read exactly MetaInt audio bytes
                    var bytesRead = await ReadExactAsync(audioBuffer, MetaInt, ct);
                    if (bytesRead == 0) break;

                    AudioDataReceived?.Invoke(this, audioBuffer[..bytesRead]);

                    // Read metadata length byte
                    if (await ReadExactAsync(metaLenBuf, 1, ct) == 0) break;

                    var metaLen = metaLenBuf[0] * 16;
                    if (metaLen > 0)
                    {
                        var metaBuffer = new byte[metaLen];
                        if (await ReadExactAsync(metaBuffer, metaLen, ct) == 0) break;

                        var metadata = IcyMetadataParser.Parse(metaBuffer);
                        if (metadata.StreamTitle is not null)
                            MetadataChanged?.Invoke(this, metadata);
                    }
                }
                else
                {
                    // No metadata — just stream audio
                    var bytesRead = await _stream!.ReadAsync(audioBuffer, ct);
                    if (bytesRead == 0) break;

                    AudioDataReceived?.Invoke(this, audioBuffer[..bytesRead]);
                }
            }

            IsConnected = false;
            Disconnected?.Invoke(this, "Stream ended");
        }
        catch (OperationCanceledException)
        {
            IsConnected = false;
            Disconnected?.Invoke(this, "Stopped by user");
        }
        catch (Exception ex)
        {
            IsConnected = false;
            // Suppress spurious errors caused by Dispose() closing the stream
            if (!_disposed)
            {
                Logger.Error(ex, "Error in stream reading loop");
                Error?.Invoke(this, ex.Message);
            }
        }
    }

    private async Task<int> ReadExactAsync(byte[] buffer, int count, CancellationToken ct)
    {
        var totalRead = 0;
        while (totalRead < count)
        {
            var read = await _stream!.ReadAsync(
                buffer.AsMemory(totalRead, count - totalRead), ct);
            if (read == 0) return 0; // Connection closed
            totalRead += read;
        }
        return totalRead;
    }

    public async Task StopAsync()
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync();
            if (_readTask is not null)
            {
                try { await _readTask; } catch (OperationCanceledException) { }
            }
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _cts?.Cancel();
        _cts?.Dispose();
        _stream?.Dispose();
        _tcpClient?.Dispose();
    }
}
