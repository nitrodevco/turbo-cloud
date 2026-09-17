using Orleans;

namespace Turbo.Primitives.Players.Snapshots;

[GenerateSerializer, Immutable]
public sealed record BadgeRarityCountSnapshot
{
    [Id(0)]
    public required byte RarityId { get; init; }

    [Id(1)]
    public required int Count { get; init; }
}
