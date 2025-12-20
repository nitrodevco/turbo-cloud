using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record GetGuestRoomMessage : IMessageEvent
{
    public RoomId RoomId { get; init; }
    public bool EnterRoom { get; init; }
    public bool RoomForward { get; init; }
}
