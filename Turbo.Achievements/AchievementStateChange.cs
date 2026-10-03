using Turbo.Primitives.Achievements.Enums;

namespace Turbo.Achievements;

/// <summary>What setting an achievement's state did, or would do on a dry run.</summary>
/// <param name="Changed">False when the achievement was already in that state, so nothing was published.</param>
public sealed record AchievementStateChange(
    string Key,
    AchievementState From,
    AchievementState To,
    int Revision,
    bool Changed
);
