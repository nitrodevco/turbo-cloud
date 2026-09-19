using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Room.Pets;

/// <summary>Apply a pet product standing in the room to a pet.</summary>
public record CustomizePetWithFurniMessage : IMessageEvent
{
    public required RoomObjectId ObjectId { get; init; }
    public required int PetId { get; init; }
}
