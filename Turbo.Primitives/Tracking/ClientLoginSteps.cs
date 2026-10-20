namespace Turbo.Primitives.Tracking;

/// <summary>
/// The steps of a client's login, named as Habbo names them (<c>HabboLoginTrackingStep.as</c>).
/// The server records the ones it sees itself: the socket opening, the handshake, the ticket
/// accepted and the first room entered.
/// </summary>
public static class ClientLoginSteps
{
    public const string SOCKET_OK = "client.init.socket.ok";
    public const string HANDSHAKE_START = "client.init.handshake.start";
    public const string HANDSHAKE_OK = "client.init.handshake.ok";
    public const string AUTH_OK = "client.init.auth.ok";
    public const string ROOM_ENTER = "client.init.room.enter";

    /// <summary>The steps in the order a login goes through them, for the login funnel.</summary>
    public static readonly string[] Ordered =
    [
        SOCKET_OK,
        HANDSHAKE_START,
        HANDSHAKE_OK,
        AUTH_OK,
        ROOM_ENTER,
    ];
}
