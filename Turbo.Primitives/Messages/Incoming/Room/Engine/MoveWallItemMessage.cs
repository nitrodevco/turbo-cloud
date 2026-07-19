using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

public record MoveWallItemMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required string WallPosition { get; init; }
}
