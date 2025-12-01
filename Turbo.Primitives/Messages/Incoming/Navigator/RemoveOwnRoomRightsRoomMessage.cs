using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Navigator;

public record RemoveOwnRoomRightsRoomMessage : IMessageEvent
{
    public int RoomId { get; init; }
}
