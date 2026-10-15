namespace Turbo.Primitives.Hotel.Enums;

/// <summary>
/// Where the concurrent users goal stands for a player, as the reception's
/// <c>ConcurrentUsersInfoElementHandler</c> reads it.
/// </summary>
public enum ConcurrentUsersGoalState
{
    /// <summary>The hotel has no goal set.</summary>
    Disabled = 0,

    /// <summary>The goal is not reached yet.</summary>
    Active = 1,

    /// <summary>The goal is reached and the player has a reward to take.</summary>
    Redeem = 2,

    /// <summary>The goal is reached and the player has nothing to take (any more).</summary>
    Rewarded = 3,
}
