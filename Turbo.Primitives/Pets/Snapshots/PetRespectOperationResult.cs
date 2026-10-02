using Orleans;

namespace Turbo.Primitives.Pets.Snapshots;

[GenerateSerializer, Immutable]
public sealed record PetRespectOperationResult
{
    [Id(0)]
    public required bool Accepted { get; init; }

    [Id(1)]
    public int Respect { get; init; }
}
