using Turbo.Primitives.Achievements.Enums;

namespace Turbo.Primitives.Achievements;

/// <summary>
/// What a definition's <see cref="AchievementState"/> means. Ask these instead of comparing the
/// state, so every place that decides who accrues progress or who is shown an achievement agrees.
/// </summary>
public static class AchievementDefinitionExtensions
{
    /// <summary>Only an enabled achievement is bound to new facts and awards new levels.</summary>
    public static bool Accrues(this AchievementDefinition definition) =>
        definition.State == AchievementState.Enabled;

    /// <summary>
    /// The client can be told about it: every state but disabled (hidden) and wired-controlled
    /// (not supported). Badge point limits cover all of these.
    /// </summary>
    public static bool IsKnownToClient(this AchievementDefinition definition) =>
        definition.State
            is AchievementState.Enabled
                or AchievementState.Archived
                or AchievementState.OffSeason;

    /// <summary>
    /// Whether this player's achievement list contains it. An archived achievement only matters to
    /// a player who progressed it, so a new account does not see a wall of things it can never earn.
    /// </summary>
    public static bool IsListedFor(this AchievementDefinition definition, bool showArchived) =>
        definition.State == AchievementState.Archived ? showArchived : definition.IsKnownToClient();
}
