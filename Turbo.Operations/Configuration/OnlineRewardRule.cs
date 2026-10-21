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

    public bool Enabled { get; init; } = true;
}
