using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SuperSocket.ProtoBase;
using SuperSocket.WebSocket.Server;
using Turbo.Primitives.Crypto;
using Turbo.Primitives.Networking;
using Turbo.Runtime;

namespace Turbo.Networking.Session;

public class WebSocketSessionContext(
    IPackageEncoder<OutgoingPackage> packageEncoder,
    ILogger<ISessionContext> logger
) : WebSocketSession(), ISessionContext
{
    private readonly IPackageEncoder<OutgoingPackage> _packageEncoder = packageEncoder;
    private readonly SessionContextState _state = new(logger);

    public SessionKey SessionKey => this.SessionID;
    public bool PolicyDone
    {
        get => _state.PolicyDone;
        set => _state.PolicyDone = value;
    }
    public string RevisionId => _state.RevisionId;
    public DateTime LastActivityUtc => _state.LastActivityUtc;
    public AsyncSignal PongWaiter => _state.PongWaiter;
    public CancellationTokenSource HeartbeatCts => _state.HeartbeatCts;
    public IRc4Engine? CryptoIn => _state.CryptoIn;
    public IRc4Engine? CryptoOut => _state.CryptoOut;

    public ArrayBufferWriter<byte>? WsBuffer { get; } = new(4096);

    public async Task CloseSessionAsync() => await this.CloseAsync().ConfigureAwait(false);

    public void Touch() => _state.Touch();

    public void SetRevisionId(string revisionId) => _state.RevisionId = revisionId;

    public void SetupEncryption(byte[] key, bool setCryptoOut = false) =>
        _state.SetupEncryption(key, setCryptoOut);

    public Task SendComposerAsync(IComposer composer, CancellationToken ct) =>
        _state.SendAsync(this, composer, token => SendEncodedAsync(composer, token), ct);

    // A WebSocket frame carries the whole packet, so it is encoded into a buffer and sent as one
    // binary message rather than written through the connection's pipe like TCP.
    private ValueTask SendEncodedAsync(IComposer composer, CancellationToken ct)
    {
        var buffer = new ArrayBufferWriter<byte>();

        _packageEncoder.Encode(buffer, new OutgoingPackage(this, composer));

        return SendAsync(buffer.WrittenMemory.ToArray(), ct);
    }
}
