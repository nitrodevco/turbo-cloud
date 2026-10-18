using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Database.Entities.Movements;

/// <summary>
/// One <see cref="AssetMovement"/>, written in the same save as the change it describes, so the
/// outbox never holds a change that did not commit nor misses one that did. Read in id order by
/// whatever consumes it; append-only but for pruning (which deletes the oldest rows and turns the
/// newest of them into an <c>outbox.pruned</c> marker), and without foreign keys or a unique key: it outlives the
/// players, rooms and items it names, and a retried save writes its rows again (consumers drop a
/// repeated operation and seq). See <c>docs/movements.md</c>.
/// </summary>
[Table("asset_movement_outbox")]
// Pruning by age.
[Index(nameof(CreatedAt))]
public class AssetMovementOutboxEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("operation_id")]
    [MaxLength(AssetMovement.MAX_OPERATION_ID_LENGTH)]
    public required string OperationId { get; set; }

    [Column("seq")]
    public short Seq { get; set; }

    [Column("occurred_at")]
    public DateTime OccurredAt { get; set; }

    [Column("asset_kind")]
    public AssetKind AssetKind { get; set; }

    [Column("asset_id")]
    public long AssetId { get; set; }

    [Column("definition_id")]
    public int DefinitionId { get; set; }

    [Column("currency_type")]
    public CurrencyType? CurrencyType { get; set; }

    [Column("activity_point_type")]
    public int? ActivityPointType { get; set; }

    [Column("from_owner")]
    public int FromOwner { get; set; }

    [Column("from_kind")]
    public PlaceKind FromKind { get; set; }

    [Column("from_place")]
    public long FromPlace { get; set; }

    [Column("to_owner")]
    public int ToOwner { get; set; }

    [Column("to_kind")]
    public PlaceKind ToKind { get; set; }

    [Column("to_place")]
    public long ToPlace { get; set; }

    /// <summary>The room end's position: where it was put, or else where it was taken from.</summary>
    [Column("x")]
    public int? X { get; set; }

    [Column("y")]
    public int? Y { get; set; }

    [Column("z")]
    public double? Z { get; set; }

    [Column("rot")]
    public Rotation? Rotation { get; set; }

    [Column("wall_offset")]
    public int? WallOffset { get; set; }

    [Column("amount")]
    public long Amount { get; set; }

    [Column("actor")]
    public int Actor { get; set; }

    [Column("cause")]
    [MaxLength(AssetMovement.MAX_CAUSE_LENGTH)]
    public required string Cause { get; set; }

    [Column("cause_ref")]
    [MaxLength(AssetMovement.MAX_CAUSE_REF_LENGTH)]
    public string? CauseRef { get; set; }

    /// <summary>
    /// The snapshot's extra data, on a movement that created or destroyed a furni; empty for a
    /// snapshot without any, null for a movement without a snapshot.
    /// </summary>
    [Column("snapshot_extra_data", TypeName = "longtext")]
    [MaxLength(int.MaxValue)]
    public string? SnapshotExtraData { get; set; }

    /// <summary>UTC; when the row was added to its save.</summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
