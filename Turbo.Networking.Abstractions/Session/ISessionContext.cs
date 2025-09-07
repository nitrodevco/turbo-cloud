using System.Threading;
using System.Threading.Tasks;
using SuperSocket.Server.Abstractions.Session;
using Turbo.Contracts.Abstractions;
using Turbo.Networking.Abstractions.Encryption;

namespace Turbo.Networking.Abstractions.Session;

public interface ISessionContext : IAppSession
{
    public bool PolicyDone { get; set; }
    public string RevisionId { get; }
    public long PlayerId { get; }
    public IStreamCipher Rc4Engine { get; }
    public void SetRevisionId(string revisionId);
    public void SetPlayerId(long playerId);
    public void SetupEncryption(byte[] key);
    public Task SendComposerAsync(IComposer composer, CancellationToken ct = default);
}
