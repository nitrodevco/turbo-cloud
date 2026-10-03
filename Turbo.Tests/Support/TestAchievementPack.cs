using System.Collections.Immutable;
using Turbo.Primitives.Achievements;

namespace Turbo.Tests.Support;

/// <summary>A pack with exactly the ranges and definitions a test gives it.</summary>
public sealed class TestAchievementPack(
    string key,
    int version,
    AchievementIdRange[] idRanges,
    AchievementDefinition[] definitions
) : IAchievementPack
{
    public string Key => key;

    public int Version => version;

    public ImmutableArray<AchievementIdRange> IdRanges { get; } = [.. idRanges];

    public ImmutableArray<AchievementDefinition> Definitions { get; } = [.. definitions];
}
