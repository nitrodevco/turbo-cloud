using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Orleans.Snapshots.Players;

[GenerateSerializer, Immutable]
public sealed record PlayerWalletSnapshot
{
    [Id(0)]
    public required int Credits { get; init; }

    [Id(1)]
    public required ImmutableDictionary<int, int> ActivityPointsByCategoryId { get; init; }

    [Id(2)]
    public required int Emeralds { get; init; }

    [Id(3)]
    public required int Silver { get; init; }
}
