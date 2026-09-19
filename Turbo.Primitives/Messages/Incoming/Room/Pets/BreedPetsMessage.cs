using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Enums;

namespace Turbo.Primitives.Messages.Incoming.Room.Pets;

/// <summary>Request, cancel or accept breeding between two monsterplants.</summary>
public record BreedPetsMessage : IMessageEvent
{
    public required PetBreedingAction Action { get; init; }
    public required int PetId { get; init; }
    public required int OtherPetId { get; init; }
}
