using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Roomdirectory;

/// <summary>
/// A room session opened on a room network (<c>RoomSessionManager.gotoRoomNetwork</c>), in place
/// of a room's own open connection: the server picks the network's room.
/// </summary>
public record RoomNetworkOpenConnectionMessage : IMessageEvent
{
    public required int NetworkId { get; init; }

    /// <summary>The player's home room when they asked to go there, else 0.</summary>
    public required int HomeRoomId { get; init; }
}
