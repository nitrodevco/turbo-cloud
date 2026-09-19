using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Inventory.Pets;

public record CancelPetBreedingMessage : IMessageEvent
{
    public required RoomObjectId NestId { get; init; }
}
