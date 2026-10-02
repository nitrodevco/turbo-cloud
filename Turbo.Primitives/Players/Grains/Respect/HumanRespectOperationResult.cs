using Orleans;

namespace Turbo.Primitives.Players.Grains.Respect;

[GenerateSerializer, Immutable]
public sealed record HumanRespectOperationResult
{
    [Id(0)]
    public required bool Accepted { get; init; }

    [Id(1)]
    public int TargetRespectTotal { get; init; }
}
