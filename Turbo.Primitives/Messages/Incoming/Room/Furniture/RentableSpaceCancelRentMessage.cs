using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Furniture;

/// <summary>Gives a rented space up before its time.</summary>
public record RentableSpaceCancelRentMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
}
