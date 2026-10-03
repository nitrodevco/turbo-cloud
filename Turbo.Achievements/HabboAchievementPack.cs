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

    public int Version => 2;

    /// <summary>The hand-mapped records (1001-1018), then every other published record at 10000 plus its API id.</summary>
    public ImmutableArray<AchievementIdRange> IdRanges { get; } =
    [
        new(1001, 1018),
        new(AchievementDefaults.UNHOOKED_ID_START, AchievementDefaults.UNHOOKED_ID_START + 9999),
    ];

    public ImmutableArray<AchievementDefinition> Definitions { get; } =
    [.. AchievementDefaults.Definitions, .. AchievementDefaults.Unhooked];
}
