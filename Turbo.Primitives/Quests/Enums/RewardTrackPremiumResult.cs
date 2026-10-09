namespace Turbo.Primitives.Quests.Enums;

/// <summary>
/// A premium purchase's result, the int the client turns into
/// <c>reward_track.premium.notification.fail.&lt;code&gt;</c> (0 is success).
/// </summary>
public enum RewardTrackPremiumResult
{
    Success = 0,

    /// <summary>"Reward tracks are currently disabled".</summary>
    Disabled = 1,

    /// <summary>"Reward track not found".</summary>
    TrackNotFound = 2,

    /// <summary>"You are not eligible for premium on this track".</summary>
    NotEligible = 3,

    /// <summary>"Premium is not configured for this track".</summary>
    NotConfigured = 4,

    /// <summary>"You already own premium for this track".</summary>
    AlreadyOwned = 5,

    /// <summary>"Premium could not be purchased because the configuration is invalid".</summary>
    InvalidConfig = 6,

    /// <summary>"You do not have enough credits".</summary>
    NotEnoughCredits = 7,

    /// <summary>"You do not have enough diamonds".</summary>
    NotEnoughDiamonds = 8,

    /// <summary>"Failed to unlock premium track".</summary>
    Failed = 9,
}
