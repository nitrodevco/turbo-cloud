using System;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Wardrobe;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Database.Extensions;

/// <summary>Rows a player owns (wardrobe, wallet, subscriptions) and the currency types the wallet is keyed by.</summary>
public static class PlayerEntityExtensions
{
    public static OutfitDataSnapshot ToSnapshot(this PlayerOutfitEntity entity) =>
        new()
        {
            SlotId = entity.SlotId,
            Figure = entity.Figure,
            Gender = entity.Gender,
        };

    /// <param name="kind">The currency the row's type id stands for, resolved by the caller.</param>
    public static WalletCurrencySnapshot ToSnapshot(
        this PlayerCurrencyEntity entity,
        CurrencyKind kind
    ) =>
        new()
        {
            Id = entity.Id,
            CurrencyKind = kind,
            Amount = entity.Amount,
        };

    /// <summary>
    /// A subscription row as it stands at <paramref name="now"/>. The countdowns are worked out
    /// here rather than stored, and a row that has never been bought reads as a lapsed one.
    /// </summary>
    /// <param name="now">The moment the countdowns are measured from.</param>
    /// <param name="graceDays">Days the hotel keeps a lapsed subscription in grace.</param>
    /// <param name="maxFurniLimit">The Builders Club borrow ceiling the hotel is configured with.</param>
    public static PlayerSubscriptionSnapshot ToSnapshot(
        this PlayerSubscriptionEntity entity,
        DateTime now,
        int graceDays,
        int maxFurniLimit
    )
    {
        var secondsLeft = SecondsUntil(entity.ExpiresAt, now);

        return new()
        {
            SubscriptionType = entity.SubscriptionType,
            SecondsLeft = secondsLeft,
            SecondsLeftWithGrace = SecondsUntil(entity.ExpiresAt?.AddDays(graceDays), now),
            HasEverBeenMember = entity.FirstSubscribedAt is not null,
            TotalDaysSubscribed = entity.TotalDaysSubscribed,
            DaysRemaining = (int)Math.Ceiling(secondsLeft / (double)SECONDS_PER_DAY),
            FurniLimit = entity.FurniLimit,
            MaxFurniLimit = maxFurniLimit,
            MinutesSinceLastModified = MinutesSince(entity.UpdatedAt, now),
            ExpiresAt = entity.ExpiresAt,
        };
    }

    private const int SECONDS_PER_DAY = 60 * 60 * 24;

    /// <summary>
    /// Minutes since a row was written, or -1 when it has never been written: a row the player
    /// has never bought is built in memory and carries no timestamp at all.
    /// </summary>
    private static int MinutesSince(DateTime writtenAt, DateTime now) =>
        writtenAt == default ? -1 : (int)Math.Max(0, (now - writtenAt).TotalMinutes);

    /// <summary>Whole seconds from <paramref name="now"/> until a deadline, never below zero.</summary>
    private static int SecondsUntil(DateTime? deadline, DateTime now)
    {
        if (deadline is not { } at || at <= now)
            return 0;

        var seconds = (at - now).TotalSeconds;

        // The client reads this into an int and counts down from it; a subscription further off
        // than int.MaxValue seconds is a bad row, not a reason to overflow.
        return seconds >= int.MaxValue ? int.MaxValue : (int)seconds;
    }

    public static CurrencyTypeSnapshot ToSnapshot(this CurrencyTypeEntity entity) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name ?? string.Empty,
            CurrencyType = entity.CurrencyType,
            ActivityPointType = entity.ActivityPointType,
            Enabled = entity.Enabled,
        };
}
