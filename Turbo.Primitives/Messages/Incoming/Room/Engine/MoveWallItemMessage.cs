using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

public record MoveWallItemMessage : IMessageEvent
{
    public required int ObjectId { get; init; }
    public required string WallPosition { get; init; }
}
