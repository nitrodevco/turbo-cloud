using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

public record PlacePostItMessage : IMessageEvent
{
    public required RoomObjectId ItemId { get; init; }
    public required string Location { get; init; }
}
