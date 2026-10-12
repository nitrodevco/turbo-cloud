using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// Every time a subscription was extended: bought, given by staff, or granted by a rank. The
/// subscription row holds where it stands now; this is how it got there, so a disputed purchase or
/// a gift can be looked up. Written in the same transaction as the extension.
/// </summary>
[Comment(
    "Every time a player's subscription was extended: how many days, bought or given, and until when."
)]
[Table("player_subscription_grants")]
[Index(nameof(PlayerEntityId), nameof(CreatedAt))]
public class PlayerSubscriptionGrantEntity : TurboLogEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("subscription_type")]
    public required SubscriptionType SubscriptionType { get; set; }

    [Column("days")]
    public required int Days { get; set; }

    /// <summary>Paid for in the catalog; otherwise given (staff, a rank, a reward).</summary>
    [Column("purchased")]
    public required bool Purchased { get; set; }

    /// <summary>When the subscription ran out after this grant.</summary>
    [Column("expires_at")]
    public required DateTime ExpiresAt { get; set; }
}
