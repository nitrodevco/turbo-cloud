namespace Turbo.Primitives.Achievements.Enums;

/// <summary>Where a distinct achievement takes the value it counts from.</summary>
public enum AchievementValueSource
{
    /// <summary>The value the fact carries, such as a room id.</summary>
    Fact,

    /// <summary>
    /// The UTC date the fact occurred on (<c>yyyy-MM-dd</c>), so a source that carries no value of
    /// its own, such as a login, can count distinct days.
    /// </summary>
    UtcDate,
}
