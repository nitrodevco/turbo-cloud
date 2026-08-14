using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Messages.Incoming.Room.Action;

public record RemoveAllRightsMessage : IMessageEvent
{
    public RoomId RoomId { get; init; }
}
