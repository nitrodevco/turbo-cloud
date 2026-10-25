using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Furniture;

/// <summary>
/// A furni granted under a plugin's reference (<c>IHotel.Inventory.GrantFurnitureAsync</c> with a
/// reference), written in the same transaction as the item: a grant asked again under the same
/// reference for the same player (a retry after a crash) finds it and makes nothing. The item it
/// made is named, and outlives it: the receipt stays when the item is traded or deleted.
/// </summary>
[Table("furniture_grant_receipts")]
[PrimaryKey(nameof(PlayerId), nameof(Reference))]
public class FurnitureGrantReceiptEntity
{
    public const int REFERENCE_MAX_LENGTH = 128;

    [Column("player_id")]
    public int PlayerId { get; set; }

    [Column("reference")]
    [MaxLength(REFERENCE_MAX_LENGTH)]
    public required string Reference { get; set; }

    /// <summary>The furni the grant made.</summary>
    [Column("furniture_id")]
    public int FurnitureId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
