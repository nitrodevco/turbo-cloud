using System;
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

    /// <summary>
    /// When a packet last arrived on this connection (UTC). The heartbeat closes a connection that
    /// has been silent too long: a client that slept or lost its network never sends a close, so
    /// without this its player would stay online and standing in their room.
    /// </summary>
    public DateTime LastReceivedUtc { get; }

    /// <summary>
    /// Records that a packet arrived; called as each packet is framed off the wire, before it
    /// waits behind the packets still being handled.
    /// </summary>
    public void MarkReceived();

    public Task CloseSessionAsync();
    public void SetRevisionId(string revisionId);
    public void SetupEncryption(byte[] key, bool setCryptoOut = false);
    public Task SendComposerAsync(IComposer composer, CancellationToken ct);
}
