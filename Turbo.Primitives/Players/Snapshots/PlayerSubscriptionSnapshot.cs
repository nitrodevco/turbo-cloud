using System;
using Orleans;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Players.Snapshots;

/// <summary>
/// What one of a player's subscriptions looks like right now. The countdown fields are worked
/// out against the moment the snapshot was built, because that is how the client uses them: it
/// is handed a number of seconds once and counts down itself
/// (<c>HabboCatalog.refreshBuilderStatus</c>), so a snapshot is only ever read fresh and never
/// cached by a caller.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record PlayerSubscriptionSnapshot
{
    [Id(0)]
    public required SubscriptionType SubscriptionType { get; init; }

    /// <summary>Seconds until the subscription runs out; zero once it has.</summary>
    [Id(1)]
    public required int SecondsLeft { get; init; }

    /// <summary>The same, with the hotel's grace period added on.</summary>
    [Id(2)]
    public required int SecondsLeftWithGrace { get; init; }

    /// <summary>Whether the player has ever held this subscription, expired or not.</summary>
    [Id(3)]
    public required bool HasEverBeenMember { get; init; }

    /// <summary>Days ever granted, across every purchase.</summary>
    [Id(4)]
    public required int TotalDaysSubscribed { get; init; }

    /// <summary>Whole days still to run, rounded up; zero once the subscription has lapsed.</summary>
    [Id(5)]
    public required int DaysRemaining { get; init; }

    /// <summary>Builders Club only: how many items this player may borrow at once.</summary>
    [Id(6)]
    public required int FurniLimit { get; init; }

    /// <summary>Builders Club only: the ceiling extensions raise <see cref="FurniLimit"/> to.</summary>
    [Id(7)]
    public required int MaxFurniLimit { get; init; }

    /// <summary>Minutes since the row last changed, or -1 when the player has no row at all.</summary>
    [Id(8)]
    public required int MinutesSinceLastModified { get; init; }

    [Id(9)]
    public required DateTime? ExpiresAt { get; init; }

    public bool IsActive => SecondsLeft > 0;

    /// <summary>Lapsed, but still inside the hotel's grace period.</summary>
    public bool IsInGrace => SecondsLeft <= 0 && SecondsLeftWithGrace > 0;

    /// <summary>Days already used up, which is what the club centre and club gifts count.</summary>
    public int DaysConsumed => Math.Max(0, TotalDaysSubscribed - DaysRemaining);

    public int MinutesUntilExpiration => (int)(SecondsLeft / 60d);
}
