using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Incoming.RoomSettings;

public record DeleteRoomMessage : IMessageEvent
{
    public RoomId RoomId { get; init; }
}
