using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Room.Pets;

/// <summary>Refreshes the context-menu flags of a placed pet without a full info stand.</summary>
[GenerateSerializer, Immutable]
public sealed record PetStatusUpdateMessageComposer : IComposer
{
    [Id(0)]
    public required RoomObjectId ObjectId { get; init; }

    [Id(1)]
    public required int PetId { get; init; }

    [Id(2)]
    public required bool CanBreed { get; init; }

    [Id(3)]
    public required bool CanHarvest { get; init; }

    [Id(4)]
    public required bool CanRevive { get; init; }

    [Id(5)]
    public required bool HasBreedingPermission { get; init; }
}
