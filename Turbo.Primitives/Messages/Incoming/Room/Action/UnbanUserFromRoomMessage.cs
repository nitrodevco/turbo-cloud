using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Incoming.Room.Action;

public record UnbanUserFromRoomMessage : IMessageEvent
{
    public int UserId { get; init; }
    public RoomId RoomId { get; init; }
}
