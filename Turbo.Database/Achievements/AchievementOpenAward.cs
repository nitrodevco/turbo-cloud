using System;

namespace Turbo.Database.Achievements;

/// <summary>
/// One earned level that is not finished yet: either its rewards, badge and score are still being
/// delivered, or they are delivered and the client has not been told. It freezes the definition
/// revision the level was earned under, so a later catalog cannot change what was promised. A level
/// is forgotten once the player has been told; the progress row keeps the totals.
/// </summary>
public sealed record AchievementOpenAward
{
    public required int Level { get; init; }
    public required int Revision { get; init; }
    public required DateTime EarnedAtUtc { get; init; }

    /// <summary>Rewards of this level already delivered, in order.</summary>
    public int DeliveredRewards { get; init; }

    /// <summary>Rewards, badge and score are all durably delivered; only the notice is left.</summary>
    public bool Completed { get; init; }

    public string? BlockedReason { get; init; }
}
