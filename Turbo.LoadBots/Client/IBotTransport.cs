using System;
using System.Buffers;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.LoadBots.Client;

/// <summary>A byte pipe to the server: raw TCP, or binary WebSocket frames.</summary>
public interface IBotTransport : IAsyncDisposable
{
    public Task ConnectAsync(CancellationToken ct);

    public ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken ct);

    /// <summary>Reads what has arrived; zero bytes means the server closed the connection.</summary>
    public ValueTask<int> ReceiveAsync(Memory<byte> buffer, CancellationToken ct);
}

/// <summary>The game socket the Flash client opens (<c>serverOptions:TcpServer</c>).</summary>
public sealed class TcpBotTransport(string host, int port) : IBotTransport
{
    private readonly TcpClient _client = new() { NoDelay = true };
    private NetworkStream? _stream;

    public async Task ConnectAsync(CancellationToken ct)
    {
        await _client.ConnectAsync(host, port, ct);
        _stream = _client.GetStream();
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken ct) =>
        Stream.WriteAsync(frame, ct);

    public ValueTask<int> ReceiveAsync(Memory<byte> buffer, CancellationToken ct) =>
        Stream.ReadAsync(buffer, ct);

    private NetworkStream Stream =>
        _stream ?? throw new InvalidOperationException("The transport is not connected.");

    public ValueTask DisposeAsync()
    {
        _stream?.Dispose();
        _client.Dispose();

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// The socket the web client opens (<c>serverOptions:WebSocketServer</c>). The server accepts
/// binary frames only and keeps the same length-prefixed framing inside them.
/// </summary>
public sealed class WebSocketBotTransport(Uri uri) : IBotTransport
{
    private readonly ClientWebSocket _socket = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public Task ConnectAsync(CancellationToken ct) => _socket.ConnectAsync(uri, ct);

    public async ValueTask SendAsync(ReadOnlyMemory<byte> frame, CancellationToken ct)
    {
        // A WebSocket allows one send at a time.
        await _sendLock.WaitAsync(ct);

        try
        {
            await _socket.SendAsync(frame, WebSocketMessageType.Binary, true, ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    public async ValueTask<int> ReceiveAsync(Memory<byte> buffer, CancellationToken ct)
    {
        var result = await _socket.ReceiveAsync(buffer, ct);

        return result.MessageType == WebSocketMessageType.Close ? 0 : result.Count;
    }

    public async ValueTask DisposeAsync()
    {
        if (_socket.State == WebSocketState.Open)
        {
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, timeout.Token);
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
            {
                // Closing politely is best effort; the socket is disposed either way.
            }
        }

        _socket.Dispose();
        _sendLock.Dispose();
    }
}

/// <summary>A growable receive buffer the connection frames messages out of.</summary>
internal sealed class ReceiveBuffer : IDisposable
{
    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
    private int _length;

    public Memory<byte> FreeSpace()
    {
        if (_buffer.Length - _length < 16 * 1024)
        {
            var larger = ArrayPool<byte>.Shared.Rent(_buffer.Length * 2);
            _buffer.AsSpan(0, _length).CopyTo(larger);
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = larger;
        }

        return _buffer.AsMemory(_length);
    }

    public void Advance(int count) => _length += count;

    public ReadOnlySpan<byte> Data => _buffer.AsSpan(0, _length);

    public void Consume(int count)
    {
        _buffer.AsSpan(count, _length - count).CopyTo(_buffer);
        _length -= count;
    }

    public void Dispose() => ArrayPool<byte>.Shared.Return(_buffer);
}
