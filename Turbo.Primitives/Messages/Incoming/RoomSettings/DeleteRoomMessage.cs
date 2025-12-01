using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.RoomSettings;

public record DeleteRoomMessage : IMessageEvent
{
    public int RoomId { get; init; }
}
