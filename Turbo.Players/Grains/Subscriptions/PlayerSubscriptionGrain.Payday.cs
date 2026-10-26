using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Snapshots;

namespace Turbo.Players.Grains.Subscriptions;

/// <summary>
/// HC Payday as the club centre shows it. Turbo counts the catalogue credits a player spends
/// towards each payday; it pays no payday yet, so nothing has been rewarded.
/// </summary>
internal sealed partial class PlayerSubscriptionGrain
{
    private const int MINUTES_PER_DAY = 24 * 60;

    public async Task<HcPaydaySnapshot> GetHcPaydayAsync(CancellationToken ct)
    {
        var club = await SnapshotAsync(SubscriptionType.HabboClub, ct);
        var today = Today();
        var payday = HcPaydayCalendar.Next(today, _subscriptionConfig.PaydayDayOfMonth);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var row = await dbCtx
            .PlayerHcPaydays.AsNoTracking()
            .FirstOrDefaultAsync(x => x.PlayerEntityId == PlayerId.Value, ct);

        if (row is not null && row.Payday != payday)
            row = null;

        var spent = row?.CreditsSpent ?? 0;
        var percentage = _subscriptionConfig.PaydayKickbackPercentage;
        var streak = club.CurrentStreakDays;

        return new HcPaydaySnapshot
        {
            CurrentStreakDays = streak,
            FirstSubscribedAt = club.FirstSubscribedAt,
            KickbackPercentage = percentage,
            CreditsMissed = row?.CreditsMissed ?? 0,
            CreditsRewarded = 0,
            CreditsSpent = spent,
            // Habbo gives the member at a 13 day streak 5 and the one a minute into a new run
            // none (official client captures, 2026-10-26 and 2026-10-13).
            StreakBonusCredits =
                club.IsActive && streak > 0 ? _subscriptionConfig.PaydayStreakBonusCredits : 0,
            // The breakdown shows the share of the spend as "rounded from" its exact value
            // (ClubSpecialInfoBubbleView); rounded down here, which no capture settles (inference).
            SpendRewardCredits = (int)Math.Floor(spent * percentage),
            MinutesUntilPayday = (payday.DayNumber - today.DayNumber) * MINUTES_PER_DAY,
        };
    }

    public async Task RecordCatalogSpendAsync(int credits, CancellationToken ct)
    {
        if (credits <= 0)
            return;

        var isMember = (await SnapshotAsync(SubscriptionType.HabboClub, ct)).IsActive;
        var payday = HcPaydayCalendar.Next(Today(), _subscriptionConfig.PaydayDayOfMonth);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var row = await dbCtx.PlayerHcPaydays.FirstOrDefaultAsync(
            x => x.PlayerEntityId == PlayerId.Value,
            ct
        );

        if (row is null)
        {
            row = new PlayerHcPaydayEntity { PlayerEntityId = PlayerId.Value, Payday = payday };
            dbCtx.PlayerHcPaydays.Add(row);
        }
        else if (row.Payday != payday)
        {
            row.Payday = payday;
            row.CreditsSpent = 0;
            row.CreditsMissed = 0;
        }

        if (isMember)
            row.CreditsSpent += credits;
        else
            row.CreditsMissed += credits;

        await dbCtx.SaveChangesAsync(ct);
    }

    private DateOnly Today() => DateOnly.FromDateTime(_time.GetUtcNow().UtcDateTime);
}

/// <summary>When HC Payday falls: on one day of every month, UTC.</summary>
internal static class HcPaydayCalendar
{
    /// <summary>
    /// The coming payday: this month's while <paramref name="today"/> has not passed it, payday
    /// itself included, next month's after.
    /// </summary>
    public static DateOnly Next(DateOnly today, int dayOfMonth)
    {
        var payday = InMonth(today.Year, today.Month, dayOfMonth);

        if (payday >= today)
            return payday;

        var next = today.AddMonths(1);

        return InMonth(next.Year, next.Month, dayOfMonth);
    }

    private static DateOnly InMonth(int year, int month, int dayOfMonth) =>
        new(year, month, Math.Clamp(dayOfMonth, 1, DateTime.DaysInMonth(year, month)));
}
