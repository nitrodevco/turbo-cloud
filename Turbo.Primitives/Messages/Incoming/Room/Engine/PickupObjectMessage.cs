using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Engine;

public record PickupObjectMessage : IMessageEvent
{
    public required int CategoryId { get; init; }
    public required RoomObjectId ObjectId { get; init; }
    public required bool Confirm { get; init; }
}
