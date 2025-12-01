using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Crypto;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans.Snapshots.Session;

namespace Turbo.Primitives.Networking;

public interface ISessionContext
{
    public SessionKey SessionKey { get; }
    public bool PolicyDone { get; set; }
    public string RevisionId { get; }
    public DateTime LastActivityUtc { get; }
    public CancellationTokenSource HeartbeatCts { get; }
    public IRc4Engine? CryptoIn { get; }
    public IRc4Engine? CryptoOut { get; }
    public Task CloseSessionAsync();
    public void Touch();
    public void SetRevisionId(string revisionId);
    public void SetupEncryption(byte[] key, bool setCryptoOut = false);
    public Task SendComposerAsync(IComposer composer, CancellationToken ct);
}
