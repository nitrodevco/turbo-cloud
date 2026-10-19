using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Pets;

/// <summary>Apply a pet product, standing in the room or in the inventory, to a pet.</summary>
public record CustomizePetWithFurniMessage : IMessageEvent
{
    /// <summary>
    /// The product's room object id, or, used from the inventory, its inventory ref: a floor
    /// item's id negated.
    /// </summary>
    public required RoomObjectId ObjectId { get; init; }
    public required int PetId { get; init; }
}
