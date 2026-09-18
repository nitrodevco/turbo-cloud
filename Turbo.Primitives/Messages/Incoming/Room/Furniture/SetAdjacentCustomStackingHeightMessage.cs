using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

public record SetAdjacentCustomStackingHeightMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required bool Down { get; init; }
}
