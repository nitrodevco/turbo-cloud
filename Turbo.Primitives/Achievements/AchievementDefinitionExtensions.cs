using System;
using System.Globalization;
using System.Linq;
using Turbo.Primitives.Achievements.Enums;

namespace Turbo.Primitives.Achievements;

/// <summary>
/// What a definition's <see cref="AchievementState"/> and active window mean at a moment. Ask these
/// instead of comparing the state, so every place that decides who accrues progress or who is shown
/// an achievement agrees.
/// </summary>
public static class AchievementDefinitionExtensions
{
    /// <summary>
    /// The state at <paramref name="nowUtc"/>. Only an enabled definition has a window: before
    /// <see cref="AchievementDefinition.ActiveFromUtc"/> it is disabled, from
    /// <see cref="AchievementDefinition.ActiveUntilUtc"/> on it is archived. Any other state is
    /// returned as it is.
    /// </summary>
    public static AchievementState EffectiveState(
        this AchievementDefinition definition,
        DateTime nowUtc
    )
    {
        if (definition.State != AchievementState.Enabled)
            return definition.State;
        if (definition.ActiveFromUtc is { } from && nowUtc < from)
            return AchievementState.Disabled;
        if (definition.ActiveUntilUtc is { } until && nowUtc >= until)
            return AchievementState.Archived;

        return AchievementState.Enabled;
    }

    /// <summary>
    /// Whether an action at <paramref name="whenUtc"/> counts: only an enabled achievement, inside
    /// its window, is bound to new facts and awards new levels.
    /// </summary>
    public static bool Accrues(this AchievementDefinition definition, DateTime whenUtc) =>
        definition.EffectiveState(whenUtc) == AchievementState.Enabled;

    /// <summary>
    /// The client can be told about it: every state but disabled (hidden) and wired-controlled
    /// (not supported). Badge point limits cover all of these.
    /// </summary>
    public static bool IsKnownToClient(this AchievementDefinition definition, DateTime nowUtc) =>
        definition.EffectiveState(nowUtc)
            is AchievementState.Enabled
                or AchievementState.Archived
                or AchievementState.OffSeason;

    /// <summary>
    /// Whether this player's achievement list contains it. An archived achievement only matters to
    /// a player who progressed it, so a new account does not see a wall of things it can never earn.
    /// </summary>
    public static bool IsListedFor(
        this AchievementDefinition definition,
        DateTime nowUtc,
        bool showArchived
    ) =>
        definition.EffectiveState(nowUtc) == AchievementState.Archived
            ? showArchived
            : definition.IsKnownToClient(nowUtc);

    /// <summary>
    /// Whether the fact's value is one this achievement listens to. Without a value list every fact
    /// of the source matches. Checked when the fact is recorded, and frozen with its bindings.
    /// </summary>
    public static bool Matches(this AchievementDefinition definition, AchievementFact fact) =>
        definition.Match is not { Values: { IsDefaultOrEmpty: false } values }
        || values.Contains(fact.Value, StringComparer.Ordinal);

    /// <summary>
    /// The value a distinct achievement counts for this fact: the fact's own, or the UTC date it
    /// occurred on when the definition asks for that.
    /// </summary>
    public static string CountedValue(
        this AchievementDefinition definition,
        AchievementFact fact
    ) =>
        definition.Match?.ValueFrom == AchievementValueSource.UtcDate
            ? fact.OccurredAtUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : fact.Value;
}
