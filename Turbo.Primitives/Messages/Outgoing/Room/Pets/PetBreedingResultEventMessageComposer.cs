using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Pets.Snapshots;

namespace Turbo.Primitives.Messages.Outgoing.Room.Pets;

/// <summary>What each owner got out of a monsterplant breeding; sent to both.</summary>
[GenerateSerializer, Immutable]
public sealed record PetBreedingResultEventMessageComposer : IComposer
{
    [Id(0)]
    public required PetBreedingResultSnapshot Result { get; init; }

    [Id(1)]
    public required PetBreedingResultSnapshot OtherResult { get; init; }
}
