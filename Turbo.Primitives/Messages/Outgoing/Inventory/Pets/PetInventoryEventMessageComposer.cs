using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Inventory.Pets;

[GenerateSerializer, Immutable]
public sealed record PetInventoryEventMessageComposer : IComposer
{
    [Id(0)]
    public required int TotalFragments { get; init; }

    [Id(1)]
    public required int CurrentFragment { get; init; }

    /// <summary>
    /// This fragment's pets. A pet standing in a room (<see cref="PetSnapshot.RoomId"/> set) is
    /// here only in the list made for a client naming
    /// <see cref="Networking.Features.ClientFeatures.PET_INVENTORY_LOCATIONS"/>; the layout an older client
    /// reads has no place for a room and leaves such a pet out.
    /// </summary>
    [Id(2)]
    public required ImmutableArray<PetSnapshot> Pets { get; init; }

    /// <summary>The rooms the placed pets of this fragment stand in; empty when none is placed.</summary>
    [Id(3)]
    public ImmutableArray<PetRoomSnapshot> Rooms { get; init; } = [];
}
