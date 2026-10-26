namespace Turbo.Operations.Configuration;

/// <summary>One currency paid for time online.</summary>
public sealed class OnlineRewardRule
{
    /// <summary>The currency, as <c>:give</c> names it.</summary>
    public string Currency { get; init; } = string.Empty;

    /// <summary>How much each payout gives.</summary>
    public int Amount { get; init; }

    /// <summary>Minutes online between payouts.</summary>
    public int IntervalMinutes { get; init; } = 15;

    /// <summary>Paid on top of <see cref="Amount"/> to a Habbo Club member.</summary>
    public int HcBonus { get; init; }

    /// <summary>The most one player gets from this rule in a day (UTC); 0 for no limit.</summary>
    public int DailyCap { get; init; }

    /// <summary>
    /// Paid only to a player who is in a room when the payout comes round: one on the hotel view
    /// misses it.
    /// </summary>
    public bool InRoomOnly { get; init; }

    /// <summary>
    /// This rule's own <see cref="OnlineRewardsConfig.IdleMinutes"/>; null takes the hotel's.
    /// </summary>
    public int? IdleMinutes { get; init; }

    /// <summary>
    /// This rule's own <see cref="OnlineRewardsConfig.SkipSleeping"/>; null takes the hotel's.
    /// </summary>
    public bool? SkipSleeping { get; init; }

    public bool Enabled { get; init; } = true;
}
