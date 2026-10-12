using System.Collections.Generic;

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
    /// Proxies whose <c>X-Forwarded-For</c> names the player's real address, such as nginx in
    /// front of the WebSocket on the same server. A connection from anywhere else is taken at
    /// its own address, whatever header it sends.
    /// </summary>
    public List<string> TrustedProxies { get; init; } = ["127.0.0.1", "::1"];
}
