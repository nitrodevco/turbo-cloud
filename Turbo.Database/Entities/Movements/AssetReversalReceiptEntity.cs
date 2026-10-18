using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;

namespace Turbo.Database.Entities.Movements;

/// <summary>
/// One reversal asked through <c>IAssetReversalService</c>, claimed by its operation id before
/// the move: the unique key settles a retry or a race, and the row says how it went. A refused
/// reversal keeps none, so it can be asked again. No foreign keys: it outlives what it names.
/// </summary>
[Table("asset_reversal_receipts")]
[Index(nameof(OperationId), IsUnique = true)]
public class AssetReversalReceiptEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("operation_id")]
    [MaxLength(AssetMovement.MAX_OPERATION_ID_LENGTH)]
    public required string OperationId { get; set; }

    /// <summary>The operation undone, the cause ref of the reversal's movement.</summary>
    [Column("reversed_operation_id")]
    [MaxLength(AssetMovement.MAX_CAUSE_REF_LENGTH)]
    public required string ReversedOperationId { get; set; }

    [Column("actor")]
    public int Actor { get; set; }

    [Column("reason")]
    [MaxLength(ReversalRequest.MAX_REASON_LENGTH)]
    public required string Reason { get; set; }

    [Column("asset_kind")]
    public AssetKind AssetKind { get; set; }

    /// <summary>The furni moved; for one made again, its new id once it is made; 0 for a currency.</summary>
    [Column("asset_id")]
    public long AssetId { get; set; }

    /// <summary>Null while the move is under way, or when a crash left it unknown.</summary>
    [Column("outcome")]
    public ReversalOutcome? Outcome { get; set; }

    /// <summary>UTC.</summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
