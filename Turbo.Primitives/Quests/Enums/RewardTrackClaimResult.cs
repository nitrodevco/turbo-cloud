namespace Turbo.Primitives.Quests.Enums;

/// <summary>
/// A prize claim's result, the int the client turns into
/// <c>reward_track.claim.notification.fail.&lt;code&gt;</c> (0 is success).
/// </summary>
public enum RewardTrackClaimResult
{
    Success = 0,

    /// <summary>"Reward tracks are currently disabled".</summary>
    Disabled = 1,

    /// <summary>"Reward track not found".</summary>
    TrackNotFound = 2,

    /// <summary>"Reward not found".</summary>
    PrizeNotFound = 3,

    /// <summary>"You are not eligible for this reward".</summary>
    NotEligible = 4,

    /// <summary>"You do not have enough points for this reward".</summary>
    NotEnoughPoints = 5,

    /// <summary>"This reward was already claimed".</summary>
    AlreadyClaimed = 6,

    /// <summary>"Failed to claim reward".</summary>
    Failed = 7,

    /// <summary>"Premium is required for this reward".</summary>
    PremiumRequired = 8,
}
