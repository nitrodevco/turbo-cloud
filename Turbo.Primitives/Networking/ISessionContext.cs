using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using SuperSocket.Server.Abstractions.Session;
using Turbo.Primitives.Crypto;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Networking;

public interface ISessionContext : IAppSession
{
    public SessionKey SessionKey { get; }
    public bool PolicyDone { get; set; }
    public string RevisionId { get; }
    public IRc4Engine? CryptoIn { get; }
    public IRc4Engine? CryptoOut { get; }
    public ArrayBufferWriter<byte>? WsBuffer { get; }

    /// <summary>
    /// The room the player is in, as the presence grain last pushed it with its composers. It
    /// is what every incoming packet's context carries, read here so a packet does not cost a
    /// grain call; the presence grain stays the authority.
    /// </summary>
    public RoomId ActiveRoomId { get; }

    public Task CloseSessionAsync();
    public void SetRevisionId(string revisionId);
    public void SetupEncryption(byte[] key, bool setCryptoOut = false);
    public Task SendComposerAsync(IComposer composer, CancellationToken ct);
}
