using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

public record SetItemDataMessage : IMessageEvent
{
    public required RoomObjectId ItemId { get; init; }
    public required string Color { get; init; }
    public required string Text { get; init; }
}
