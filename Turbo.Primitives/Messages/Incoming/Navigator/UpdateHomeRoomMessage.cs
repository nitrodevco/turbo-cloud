using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record UpdateHomeRoomMessage : IMessageEvent
{
    public int RoomId { get; init; }
}
