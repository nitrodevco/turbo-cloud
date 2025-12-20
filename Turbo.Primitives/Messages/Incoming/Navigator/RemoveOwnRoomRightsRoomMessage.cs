using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record RemoveOwnRoomRightsRoomMessage : IMessageEvent
{
    public RoomId RoomId { get; init; }
}
