using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Database.Entities.Players;

[Table("player_subscriptions")]
[Index(nameof(PlayerEntityId), nameof(SubscriptionType), IsUnique = true)]
// The lapse sweep looks for expiring subscriptions across every player, which the unique index
// above (player first) cannot serve.
[Index(nameof(SubscriptionType), nameof(ExpiresAt))]
public class PlayerSubscriptionEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("subscription_type")]
    [DefaultValue(SubscriptionType.HabboClub)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required SubscriptionType SubscriptionType { get; set; }

    /// <summary>When the subscription runs out. Null means it never started.</summary>
    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    /// <summary>The first time this subscription was ever granted, for "has ever been a member".</summary>
    [Column("first_subscribed_at")]
    public DateTime? FirstSubscribedAt { get; set; }

    /// <summary>Days ever granted, across every purchase. Never goes down.</summary>
    [Column("total_days_subscribed")]
    [DefaultValue(0)]
    public int TotalDaysSubscribed { get; set; }

    [Column("periods_purchased")]
    [DefaultValue(0)]
    public int PeriodsPurchased { get; set; }

    /// <summary>Builders Club only: items the player may borrow at once. Zero elsewhere.</summary>
    [Column("furni_limit")]
    [DefaultValue(0)]
    public int FurniLimit { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
