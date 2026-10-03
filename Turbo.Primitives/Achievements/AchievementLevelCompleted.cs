using System;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Achievements;

/// <summary>
/// One achievement level whose rewards, badge and score are all durably delivered. It describes the
/// frozen award, so it reports the definition revision the player earned the level under.
/// </summary>
public sealed record AchievementLevelCompleted
{
    public required PlayerId PlayerId { get; init; }
    public required int AchievementId { get; init; }
    public required string Key { get; init; }
    public required string Category { get; init; }
    public required int Revision { get; init; }

    /// <summary>The level just completed, counting from one.</summary>
    public required int Level { get; init; }

    public required string BadgeCode { get; init; }
    public required int Score { get; init; }
    public required DateTime EarnedAtUtc { get; init; }
}
