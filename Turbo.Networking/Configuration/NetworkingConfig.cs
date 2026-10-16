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
    /// (<c>IncomingMessages.onPing</c>), and how often silent connections are looked for.
    /// </summary>
    public int PingIntervalMilliseconds { get; init; } = 10000;

    /// <summary>
    /// A connection that has sent nothing for this long, not even a Pong, is closed, which takes
    /// its player out of their room and offline. A client that slept or lost its network never
    /// sends a close of its own. Keep it several ping intervals long, so one slow Pong does not
    /// disconnect anybody.
    /// </summary>
    public int SessionTimeoutMilliseconds { get; init; } = 60000;

    /// <summary>
    /// A logged-in player who has done nothing for this long is told "Idle disconnection!"
    /// (DisconnectReason 112, <c>disconnected.idle</c>) and disconnected. What the client sends on
    /// its own timers (Pong, latency test, friend list and forum polls) is not doing something.
    /// 0 turns it off. Habbo's own value is not known: a recorded official session (2026-10-12)
    /// stayed connected through 107 minutes of nothing but those timers.
    /// </summary>
    public int IdleDisconnectMilliseconds { get; init; }
}
