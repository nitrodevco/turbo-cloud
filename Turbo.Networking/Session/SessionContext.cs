using System.Threading;
using System.Threading.Tasks;
using SuperSocket.Connection;
using SuperSocket.Server;
using Turbo.Contracts.Abstractions;
using Turbo.Crypto;
using Turbo.Networking.Abstractions.Session;

namespace Turbo.Networking.Session;

public class SessionContext(PacketProcessor packetProcessor) : AppSession(), ISessionContext
{
    private readonly PacketProcessor _packetProcessor = packetProcessor;

    public bool PolicyDone { get; set; } = true;
    public string RevisionId { get; private set; } = "Default";
    public long PlayerId { get; private set; }
    public Rc4Service? CryptoIn { get; private set; }
    public Rc4Service? CryptoOut { get; private set; }

    public void SetRevisionId(string revisionId)
    {
        RevisionId = revisionId;
    }

    public void SetPlayerId(long playerId)
    {
        PlayerId = playerId;
    }

    public void SetupEncryption(byte[] key, bool setCryptoOut = false)
    {
        CryptoIn = new Rc4Service(key);

        if (setCryptoOut)
            CryptoOut = new Rc4Service(key);
    }

    protected override async ValueTask OnSessionConnectedAsync()
    {
        await base.OnSessionConnectedAsync().ConfigureAwait(false);
    }

    protected override async ValueTask OnSessionClosedAsync(CloseEventArgs e)
    {
        await base.OnSessionClosedAsync(e).ConfigureAwait(false);
    }

    public async Task SendComposerAsync(IComposer composer, CancellationToken ct = default)
    {
        await _packetProcessor.ProcessComposerAsync(this, composer, ct).ConfigureAwait(false);
    }
}
