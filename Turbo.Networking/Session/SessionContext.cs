using System.Threading;
using System.Threading.Tasks;
using SuperSocket.Connection;
using SuperSocket.ProtoBase;
using SuperSocket.Server;
using Turbo.Contracts.Abstractions;
using Turbo.Crypto;
using Turbo.Networking.Abstractions.Session;
using Turbo.Networking.Encoder;

namespace Turbo.Networking.Session;

public class SessionContext(PackageEncoder packageEncoder) : AppSession(), ISessionContext
{
    private readonly PackageEncoder _packageEncoder = packageEncoder;

    public bool PolicyDone { get; set; } = true;
    public string RevisionId { get; private set; } = "Default";
    public long PlayerId { get; private set; }
    public Rc4Engine? CryptoIn { get; private set; }
    public Rc4Engine? CryptoOut { get; private set; }

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
        CryptoIn = new Rc4Engine(key);

        if (setCryptoOut)
            CryptoOut = new Rc4Engine(key);
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
