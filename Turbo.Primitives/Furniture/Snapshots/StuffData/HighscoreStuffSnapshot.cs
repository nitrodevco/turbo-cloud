using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Furniture.Snapshots.StuffData;

[GenerateSerializer, Immutable]
public sealed record HighscoreStuffSnapshot : StuffDataSnapshot
{
    [Id(0)]
    public required string Data { get; init; }

    [Id(1)]
    public required int ScoreType { get; init; }

    [Id(2)]
    public required int ClearType { get; init; }

    [Id(3)]
    public required ImmutableDictionary<int, ImmutableArray<string>> Scores { get; init; }
}
