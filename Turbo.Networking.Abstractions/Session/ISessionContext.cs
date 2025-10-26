using System;
using System.Threading;
using System.Threading.Tasks;
using SuperSocket.Server.Abstractions.Session;
using Turbo.Contracts.Abstractions;
using Turbo.Crypto;

namespace Turbo.Networking.Abstractions.Session;

public interface ISessionContext : IAppSession
{
    public bool PolicyDone { get; set; }
    public string RevisionId { get; }
    public long PlayerId { get; }
    public DateTime LastActivityUtc { get; }
    public CancellationTokenSource HeartbeatCts { get; }
    public Rc4Engine? CryptoIn { get; }
    public Rc4Engine? CryptoOut { get; }
    public void Touch();
    public void SetRevisionId(string revisionId);
    public void SetPlayerId(long playerId);
    public void SetupEncryption(byte[] key, bool setCryptoOut = false);
    public Task SendComposerAsync(IComposer composer, CancellationToken ct = default);
}
