using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

public record SetCustomStackingHeightMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required int Height { get; init; }
    public required bool? MultiWalk { get; init; }
}
