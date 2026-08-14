using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using SuperSocket.ProtoBase;
using SuperSocket.Server;
using Turbo.Crypto;
using Turbo.Primitives.Crypto;
using Turbo.Primitives.Networking;
using Turbo.Runtime;

namespace Turbo.Networking.Session;

public class TcpSessionContext(IPackageEncoder<OutgoingPackage> packageEncoder)
    : AppSession(),
        ISessionContext
{
    private readonly IPackageEncoder<OutgoingPackage> _packageEncoder = packageEncoder;
    private readonly SemaphoreSlim _sendSemaphore = new(1, 1);

    public SessionKey SessionKey => this.SessionID;
    public bool PolicyDone { get; set; } = true;
    public string RevisionId { get; private set; } = "Default";
    public DateTime LastActivityUtc { get; private set; } = DateTime.UtcNow;
    public AsyncSignal PongWaiter { get; } = new();
    public CancellationTokenSource HeartbeatCts { get; } = new();
    public IRc4Engine? CryptoIn { get; private set; }
    public IRc4Engine? CryptoOut { get; private set; }

    public ArrayBufferWriter<byte>? WsBuffer { get; } = null;

    public async Task CloseSessionAsync() => await this.CloseAsync().ConfigureAwait(false);

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

    public async Task SendComposerAsync(IComposer composer, CancellationToken ct)
    {
        await _sendSemaphore.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            if (Connection.IsClosed)
                return;

            await Connection
                .SendAsync(_packageEncoder, new OutgoingPackage(this, composer), ct)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Writing is not allowed"))
        {
            // Session was closed, ignore
            return;
        }
        catch
        {
            return;
        }
        finally
        {
            _sendSemaphore.Release();
        }
    }
}
