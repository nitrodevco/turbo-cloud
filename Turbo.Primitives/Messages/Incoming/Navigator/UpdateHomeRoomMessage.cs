using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record UpdateHomeRoomMessage : IMessageEvent
{
    public RoomId RoomId { get; init; }
}
