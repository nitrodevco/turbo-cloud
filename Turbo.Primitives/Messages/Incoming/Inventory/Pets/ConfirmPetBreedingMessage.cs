using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Incoming.Inventory.Pets;

/// <summary>An owner confirms the nest breeding and names the offspring.</summary>
public record ConfirmPetBreedingMessage : IMessageEvent
{
    public required RoomObjectId NestId { get; init; }
    public required string Name { get; init; }
    public required int PetId { get; init; }
    public required int OtherPetId { get; init; }
}
