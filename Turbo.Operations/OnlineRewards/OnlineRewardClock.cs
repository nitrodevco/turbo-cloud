using System;
using System.Collections.Generic;
using Turbo.Primitives.Networking;

namespace Turbo.Operations.OnlineRewards;

/// <summary>
/// One login's payout times: for each currency, the next whole interval of time online. A payout
/// missed (the feature off, the player idle, the server busy) is never made up: the next one is
/// the next interval still ahead.
/// </summary>
internal sealed class OnlineRewardClock(SessionKey session, DateTime onlineSinceUtc)
{
    private readonly Dictionary<int, (TimeSpan Interval, DateTime At)> _next = [];

    public SessionKey Session { get; } = session;

    /// <summary>
    /// Whether the currency's payout is due at <paramref name="now"/>; a due one moves on to the
    /// next interval. A currency seen for the first time, or with a new interval, is first due at
    /// the next whole interval online, counted from the login: now, when that is where now falls.
    /// </summary>
    public bool TakeDue(int currencyTypeId, TimeSpan interval, DateTime now)
    {
        if (!_next.TryGetValue(currencyTypeId, out var next) || next.Interval != interval)
            next = (interval, At(Intervals(now, interval, roundUp: true), interval));

        if (now < next.At)
        {
            _next[currencyTypeId] = next;

            return false;
        }

        _next[currencyTypeId] = (
            interval,
            At(Intervals(now, interval, roundUp: false) + 1, interval)
        );

        return true;
    }

    /// <summary>Forgets the currencies no rule pays any more.</summary>
    public void Keep(IReadOnlySet<int> currencyTypeIds)
    {
        // Removing while enumerating is allowed for a Dictionary.
        foreach (var id in _next.Keys)
            if (!currencyTypeIds.Contains(id))
                _next.Remove(id);
    }

    /// <summary>Whole intervals online at <paramref name="now"/>, at least one when rounded up.</summary>
    private long Intervals(DateTime now, TimeSpan interval, bool roundUp)
    {
        var online = Math.Max(0, (now - onlineSinceUtc).Ticks);
        var whole = online / interval.Ticks;

        return roundUp ? Math.Max(1, whole + (online % interval.Ticks == 0 ? 0 : 1)) : whole;
    }

    private DateTime At(long intervals, TimeSpan interval) =>
        onlineSinceUtc + TimeSpan.FromTicks(interval.Ticks * intervals);
}
