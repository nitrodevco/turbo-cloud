using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

public record AddSpamWallPostItMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required string Location { get; init; }
    public required string Color { get; init; }
    public required string Text { get; init; }
}
