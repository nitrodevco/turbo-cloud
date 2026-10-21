using System.Collections.Generic;
using Turbo.Primitives.Settings;

namespace Turbo.Operations.Configuration;

/// <summary>
/// Currency paid to players for time online: every <see cref="OnlineRewardRule.IntervalMinutes"/>
/// of their session, each player gets the rule's amount. Not part of Habbo, so it is off until a
/// hotel turns it on and gives it rules. Changes apply at once, from each player's next payout.
/// </summary>
public class OnlineRewardsConfig
{
    public const string SECTION_NAME = "Turbo:OnlineRewards";

    /// <summary>Whether players are paid for time online at all. The rules stay kept while it is off.</summary>
    [LiveSetting]
    public bool Enabled { get; init; }

    /// <summary>
    /// What is paid, one rule per currency (as <c>:give</c> names it: <c>credits</c>,
    /// <c>duckets</c>, <c>diamonds</c>...). Each player's clock starts at their login, so a rule of
    /// 15 minutes pays at 15, 30, 45 minutes online. Time offline or before a rule was on is never
    /// paid afterwards. A second rule for the same currency is ignored.
    /// </summary>
    [LiveSetting]
    public List<OnlineRewardRule> Rules { get; init; } = [];

    /// <summary>
    /// A player who has done nothing for this many minutes (moved, chatted, clicked; the client's
    /// own timers don't count) is not paid when a payout comes round. 0 leaves only
    /// <see cref="SkipSleeping"/>.
    /// </summary>
    [LiveSetting]
    public int IdleMinutes { get; init; } = 10;

    /// <summary>
    /// Not to pay a player whose avatar is asleep in its room: the room's idle sleep, or one who
    /// chose to sleep.
    /// </summary>
    [LiveSetting]
    public bool SkipSleeping { get; init; } = true;

    /// <summary>How many players are paid side by side when payouts fall due together.</summary>
    [LiveSetting]
    public int MaxConcurrentPayouts { get; init; } = 8;

    /// <summary>How often the server looks for payouts that are due, in seconds; a payout is at most this late.</summary>
    public int CheckSeconds { get; init; } = 10;
}
