using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

/// <summary>Asks what a rentable space costs and who, if anyone, rents it.</summary>
public record RentableSpaceStatusMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
}
