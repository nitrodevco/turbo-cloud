using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Inventory;

namespace Turbo.Database.Entities.Furniture;

/// <summary>
/// A furni changing owner between players, written in the same transaction as the change. The
/// furni row only knows its owner now; this is how it got there, so a player who says an item
/// was taken from them, or a duplicated rare, can be traced. Ids are plain, so the record
/// outlives the furni and the players.
/// </summary>
[Comment("A furni changing owner between players: which, from whom, to whom, and why.")]
[Table("furniture_transfers")]
// One furni's history, a player's trades either way.
[Index(nameof(FurnitureId), nameof(Id))]
[Index(nameof(FromPlayerId), nameof(Id))]
[Index(nameof(ToPlayerId), nameof(Id))]
public class FurnitureTransferEntity : TurboLogEntity
{
    [Column("furniture_id")]
    public required int FurnitureId { get; set; }

    /// <summary>What kind of furni it was, so the record reads without the furni row.</summary>
    [Column("definition_id")]
    public required int DefinitionId { get; set; }

    [Column("from_player_id")]
    public required int FromPlayerId { get; set; }

    [Column("to_player_id")]
    public required int ToPlayerId { get; set; }

    [Column("reason")]
    public required FurnitureTransferReason Reason { get; set; }
}
