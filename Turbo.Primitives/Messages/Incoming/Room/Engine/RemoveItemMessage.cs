using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

public record RemoveItemMessage : IMessageEvent
{
    public required RoomObjectId ItemId { get; init; }
}
