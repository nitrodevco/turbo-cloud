using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

/// <summary>Rents a rentable space.</summary>
public record RentableSpaceRentMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
}
