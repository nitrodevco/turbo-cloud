using System;
using System.Threading;
using System.Threading.Tasks;
using SuperSocket.Connection;
using SuperSocket.ProtoBase;
using SuperSocket.Server;
using Turbo.Contracts.Abstractions;
using Turbo.Crypto;
using Turbo.Primitives.Crypto;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Snapshots.Session;
using Turbo.Runtime;

namespace Turbo.Networking.Session;

public class SessionContext(IPackageEncoder<OutgoingPackage> packageEncoder)
    : AppSession(),
        ISessionContext
{
    private readonly IPackageEncoder<OutgoingPackage> _packageEncoder = packageEncoder;

    public SessionKey SessionKey { get; private set; } = SessionKey.Empty;
    public bool PolicyDone { get; set; } = true;
    public string RevisionId { get; private set; } = "Default";
    public DateTime LastActivityUtc { get; private set; } = DateTime.UtcNow;
    public AsyncSignal PongWaiter { get; } = new();
    public CancellationTokenSource HeartbeatCts { get; } = new();
    public IRc4Engine? CryptoIn { get; private set; }
    public IRc4Engine? CryptoOut { get; private set; }

    protected override async ValueTask OnSessionConnectedAsync()
    {
        SessionKey = SessionKey.From(this.SessionID);

        await base.OnSessionConnectedAsync().ConfigureAwait(false);
    }

    protected override async ValueTask OnSessionClosedAsync(CloseEventArgs e)
    {
        await base.OnSessionClosedAsync(e).ConfigureAwait(false);
    }

    public async ValueTask CloseSessionAsync() => await this.CloseAsync().ConfigureAwait(false);

    public void Touch()
    {
        LastActivityUtc = DateTime.UtcNow;
    }

    public void SetRevisionId(string revisionId)
    {
        RevisionId = revisionId;
    }

    public void SetupEncryption(byte[] key, bool setCryptoOut = false)
    {
        CryptoIn = new Rc4Engine(key);

        if (setCryptoOut)
            CryptoOut = new Rc4Engine(key);
    }

    public async Task SendComposerAsync(IComposer composer, CancellationToken ct = default)
    {
        try
        {
            await Connection
                .SendAsync(_packageEncoder, new OutgoingPackage(this, composer), ct)
                .ConfigureAwait(false);
        }
        catch
        {
            return;
        }
    }
}
