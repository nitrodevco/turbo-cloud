using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SuperSocket.ProtoBase;
using SuperSocket.Server;
using Turbo.Primitives.Crypto;
using Turbo.Primitives.Networking;
using Turbo.Runtime;

namespace Turbo.Networking.Session;

public class TcpSessionContext(
    IPackageEncoder<OutgoingPackage> packageEncoder,
    ILogger<ISessionContext> logger
) : AppSession(), ISessionContext
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

    public ArrayBufferWriter<byte>? WsBuffer { get; } = null;

    public async Task CloseSessionAsync() => await this.CloseAsync().ConfigureAwait(false);

    public void Touch() => _state.Touch();

    public void SetRevisionId(string revisionId) => _state.RevisionId = revisionId;

    public void SetupEncryption(byte[] key, bool setCryptoOut = false) =>
        _state.SetupEncryption(key, setCryptoOut);

    public Task SendComposerAsync(IComposer composer, CancellationToken ct) =>
        _state.SendAsync(
            this,
            composer,
            token =>
                Connection.SendAsync(_packageEncoder, new OutgoingPackage(this, composer), token),
            ct
        );
}
