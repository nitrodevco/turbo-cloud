namespace Turbo.Primitives.Tracking;

/// <summary>
/// The steps of a client's login, named as Habbo names them (<c>HabboLoginTrackingStep.as</c>).
/// The server records the ones it sees itself: the socket opening, the handshake, the ticket
/// accepted or refused, and the first room entered. Habbo's client hands its own steps to the web
/// page, not to the server, so these are the only ones kept.
/// </summary>
public static class ClientLoginSteps
{
    public const string SOCKET_OK = "client.init.socket.ok";
    public const string HANDSHAKE_START = "client.init.handshake.start";
    public const string HANDSHAKE_OK = "client.init.handshake.ok";
    public const string AUTH_OK = "client.init.auth.ok";

    /// <summary>
    /// The ticket refused: unknown, banned, or the hotel closed to the player. Named after
    /// <see cref="AUTH_OK"/> as <c>handshake.fail</c> is after <c>handshake.ok</c>; Habbo's client
    /// has no such step, since a refused client never logs in.
    /// </summary>
    public const string AUTH_FAIL = "client.init.auth.fail";
    public const string ROOM_ENTER = "client.init.room.enter";

    /// <summary>
    /// The steps in the order a login goes through them, for the login funnel; a refused ticket
    /// last, beside the funnel rather than in it.
    /// </summary>
    public static readonly string[] Ordered =
    [
        SOCKET_OK,
        HANDSHAKE_START,
        HANDSHAKE_OK,
        AUTH_OK,
        ROOM_ENTER,
        AUTH_FAIL,
    ];
}
