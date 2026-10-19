using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Players.Enums.Wallet;

namespace Turbo.Database.Entities.Movements;

/// <summary>
/// One change to a player's wallet, kept from the asset movement outbox by the core currency
/// ledger (<c>docs/movements.md</c>): who, which currency, how much (negative when it left the
/// wallet), why, and the other side of the move. A movement between two wallets is a row for
/// each. Its operation, seq and player are unique, so a movement delivered twice is kept once.
/// No foreign keys: it outlives the player.
/// </summary>
[Table("currency_ledger")]
[Index(nameof(OperationId), nameof(Seq), nameof(PlayerId), IsUnique = true)]
[Index(nameof(PlayerId), nameof(Id))]
public class CurrencyLedgerEntryEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>The outbox row it was read from.</summary>
    [Column("outbox_id")]
    public long OutboxId { get; set; }

    [Column("operation_id")]
    [MaxLength(AssetMovement.MAX_OPERATION_ID_LENGTH)]
    public required string OperationId { get; set; }

    [Column("seq")]
    public short Seq { get; set; }

    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; }

    [Column("player_id")]
    public int PlayerId { get; set; }

    [Column("currency_type")]
    public CurrencyType CurrencyType { get; set; }

    /// <summary>The activity point type, for activity points; null otherwise.</summary>
    [Column("activity_point_type")]
    public int? ActivityPointType { get; set; }

    /// <summary>Positive when it came into the wallet, negative when it left.</summary>
    [Column("amount")]
    public long Amount { get; set; }

    [Column("cause")]
    [MaxLength(AssetMovement.MAX_CAUSE_LENGTH)]
    public required string Cause { get; set; }

    [Column("cause_ref")]
    [MaxLength(AssetMovement.MAX_CAUSE_REF_LENGTH)]
    public string? CauseRef { get; set; }

    /// <summary>Who did it; 0 for the server.</summary>
    [Column("actor_id")]
    public int ActorPlayerId { get; set; }

    /// <summary>Where it came from or went to: nowhere, another wallet, an offer, a chest.</summary>
    [Column("other_kind")]
    public PlaceKind OtherKind { get; set; }

    /// <summary>The other side's owner; 0 for nobody.</summary>
    [Column("other_owner_id")]
    public int OtherOwnerId { get; set; }

    /// <summary>The offer or chest on the other side; 0 for a wallet or nowhere.</summary>
    [Column("other_place_id")]
    public long OtherPlaceId { get; set; }
}
