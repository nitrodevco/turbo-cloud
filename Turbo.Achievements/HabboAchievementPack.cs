using System.Collections.Immutable;
using Turbo.Primitives.Achievements;

namespace Turbo.Achievements;

/// <summary>
/// The shipped Habbo catalog as an ordinary pack. A hotel can turn it off with
/// <c>Turbo:Achievements:InstallDefaults</c>, and everything it installs is the hotel's to edit,
/// retire or re-goalpost.
/// </summary>
public sealed class HabboAchievementPack : IAchievementPack
{
    public const string KEY = "habbo";

    public string Key => KEY;

    public int Version => 1;

    public ImmutableArray<AchievementIdRange> IdRanges { get; } = [new(1001, 1018)];

    public ImmutableArray<AchievementDefinition> Definitions => AchievementDefaults.Definitions;
}
