namespace Turbo.Networking.Configuration;

public class NetworkingConfig
{
    public const string SECTION_NAME = "Turbo:Networking";

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
}
