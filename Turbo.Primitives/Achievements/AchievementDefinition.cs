using System;
using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Achievements.Enums;

namespace Turbo.Primitives.Achievements;

[GenerateSerializer, Immutable]
public sealed record AchievementDefinition
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required string Key { get; init; }

    [Id(2)]
    public required int Revision { get; init; }

    [Id(3)]
    public required string Category { get; init; }

    [Id(4)]
    public string SubCategory { get; init; } = "";

    [Id(5)]
    public int Order { get; init; }

    // Ids 6 and 7 were the Enabled and Archived flags, replaced by State. Never reuse them.
    [Id(8)]
    public int DisplayMethod { get; init; }

    [Id(9)]
    public required string Source { get; init; }

    [Id(10)]
    public int SourceVersion { get; init; } = 1;

    [Id(11)]
    public required AchievementReducer Reducer { get; init; }

    [Id(12)]
    public int UnitDivisor { get; init; } = 1;

    [Id(13)]
    public required ImmutableArray<AchievementLevelDefinition> Levels { get; init; }

    /// <summary>Whether the achievement accrues progress and who is shown it. See <see cref="AchievementState"/>.</summary>
    [Id(14)]
    public AchievementState State { get; init; } = AchievementState.Enabled;

    /// <summary>
    /// First instant (UTC, inclusive) an enabled achievement accrues. Before it the achievement is
    /// hidden, so a seasonal one does not sit in the list for months with nothing to do.
    /// </summary>
    [Id(15)]
    public DateTime? ActiveFromUtc { get; init; }

    /// <summary>
    /// First instant (UTC, exclusive) an enabled achievement stops accruing. After it the
    /// achievement is archived: kept by those who progressed it, hidden from everyone else.
    /// </summary>
    [Id(16)]
    public DateTime? ActiveUntilUtc { get; init; }
}
