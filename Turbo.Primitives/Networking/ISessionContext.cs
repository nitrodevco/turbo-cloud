using System;
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

    /// <summary>
    /// The room the player is in, as the presence grain last pushed it with its composers. It
    /// is what every incoming packet's context carries, read here so a packet does not cost a
    /// grain call; the presence grain stays the authority.
    /// </summary>
    public RoomId ActiveRoomId { get; }

    /// <summary>
    /// The address the client connected from, through any proxy the hotel trusts
    /// (<c>Turbo:Networking:TrustedProxies</c>); empty when it is not known. It is a player's
    /// personal data: it goes to the login's policies and events, never to a log.
    /// </summary>
    public string RemoteAddress { get; }

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

    /// <summary>
    /// When the client last sent something the player did (UTC): any message but those it sends
    /// on a timer of its own (<see cref="IClientTimerMessage"/>). The heartbeat disconnects a
    /// session idle for too long, as Habbo's <c>disconnected.idle</c> does.
    /// </summary>
    public DateTime LastActiveUtc { get; }

    /// <summary>Records that the player did something; called as each such message is parsed.</summary>
    public void MarkActive();

    /// <summary>
    /// Closes the connection, once; <paramref name="reason"/> is logged with the close so a
    /// disconnect can be explained afterwards.
    /// </summary>
    public Task CloseSessionAsync(string reason = "closed by the server");

    /// <summary>Why the server closed this connection, or null if it has not.</summary>
    public string? ServerCloseReason { get; }

    public void SetRevisionId(string revisionId);
    public void SetupEncryption(byte[] key, bool setCryptoOut = false);
    public Task SendComposerAsync(IComposer composer, CancellationToken ct);
}
