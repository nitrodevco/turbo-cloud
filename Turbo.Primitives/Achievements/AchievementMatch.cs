using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Achievements.Enums;

namespace Turbo.Primitives.Achievements;

/// <summary>
/// Declarative narrowing of the facts an achievement listens to, so a hotel can define one in data
/// without code. It never runs anything: it compares a value or picks which value is counted.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record AchievementMatch
{
    /// <summary>
    /// When not empty, only facts whose value is one of these are bound (for example the ids of
    /// the rooms a "visit the Christmas rooms" achievement counts). Compared exactly.
    /// </summary>
    [Id(0)]
    public ImmutableArray<string> Values { get; init; } = [];

    /// <summary>Which value a distinct achievement counts. See <see cref="AchievementValueSource"/>.</summary>
    [Id(1)]
    public AchievementValueSource ValueFrom { get; init; } = AchievementValueSource.Fact;
}
