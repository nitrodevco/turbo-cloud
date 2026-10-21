namespace Turbo.Networking.Configuration;

public class NetworkingConfig
{
    public const string SECTION_NAME = "Turbo:Networking";

    /// <summary>
    /// Whether the TCP game server (Flash-style clients) is started. A hotel whose clients all
    /// connect over WebSocket (Nitro) turns it off and opens one listener fewer.
    /// </summary>
    public bool TcpEnabled { get; init; } = true;

    /// <summary>
    /// How often every logged-in connection is sent a Ping, which the client answers with a Pong
    /// (<c>IncomingMessages.onPing</c>), and how often silent connections are looked for. Habbo
    /// pings every 60 to 90 seconds (median 60 s over three recorded official sessions).
    /// </summary>
    public int PingIntervalMilliseconds { get; init; } = 60000;

    /// <summary>
    /// A connection that has sent nothing for this long, not even a Pong, is closed, which takes
    /// its player out of their room and offline. A client that slept or lost its network never
    /// sends a close of its own. Keep it several ping intervals long, so one slow Pong does not
    /// disconnect anybody.
    /// </summary>
    public int SessionTimeoutMilliseconds { get; init; } = 180000;

    /// <summary>
    /// A logged-in player who has done nothing for this long is told "Idle disconnection!"
    /// (DisconnectReason 112, <c>disconnected.idle</c>) and disconnected. What the client sends on
    /// its own timers (Pong, latency test, friend list and forum polls) is not doing something.
    /// 0 turns it off. Habbo's own value is not known: a recorded official session (2026-10-12)
    /// stayed connected through 107 minutes of nothing but those timers.
    /// </summary>
    public int IdleDisconnectMilliseconds { get; init; }

    /// <summary>
    /// The most bytes one incoming packet may declare: its 2-byte header and body, not counting
    /// the 4-byte length in front. A connection that declares more, or less than a header, is
    /// closed before anything is buffered for it, and a WebSocket connection is never let hold
    /// more than one such packet unread, so a client, logged in or not, cannot make the server
    /// keep memory for a packet it never finishes. The largest a client sends are a few hundred
    /// KB: a photo's zlib render data (<c>RenderRoomMessage</c>), the starter room picture
    /// (render data and a 296x284 PNG) and a bug report (JSON and a JPEG at most 1280 px wide).
    /// </summary>
    public int MaxIncomingPacketBytes { get; init; } = 2 * 1024 * 1024;
}
