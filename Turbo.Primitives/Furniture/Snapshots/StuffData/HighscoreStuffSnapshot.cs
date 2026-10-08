using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Furniture.Snapshots.StuffData;

/// <summary>
/// A highscore board's stuff data, as Flash's <c>HighScoreStuffData</c> reads it: the state, the
/// score type and clear type, then each row in the order the board lists them.
/// </summary>
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
    public required ImmutableArray<HighscoreEntrySnapshot> Entries { get; init; }
}

/// <summary>One row of a highscore board: its score (seconds for the time boards) and who.</summary>
[GenerateSerializer, Immutable]
public sealed record HighscoreEntrySnapshot
{
    [Id(0)]
    public required int Score { get; init; }

    [Id(1)]
    public required ImmutableArray<string> Users { get; init; }
}
