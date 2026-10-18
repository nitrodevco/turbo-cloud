using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Catalog;

/// <summary>
/// A marketplace offer: one item taken out of its seller's inventory, kept here as its definition
/// and extra data until it is bought (the row stays, sold, for the price history and the seller's
/// credits) or taken back (the row goes and the item returns).
/// </summary>
[Table("marketplace_offers")]
[Index(nameof(State), nameof(FurnitureDefinitionEntityId))]
[Index(nameof(PlayerEntityId), nameof(State))]
public class MarketplaceOfferEntity : TurboEntity
{
    public const int STATE_OPEN = 1;
    public const int STATE_SOLD = 2;

    /// <summary>The seller.</summary>
    [Column("player_id")]
    public int PlayerEntityId { get; set; }

    [Column("definition_id")]
    public int FurnitureDefinitionEntityId { get; set; }

    /// <summary>
    /// The id the item's furniture row had when it was listed (the row is gone; no foreign key), so
    /// the movement that takes it off the offer names the asset the listing put there. Null for an
    /// offer listed before it was kept.
    /// </summary>
    [Column("furniture_id")]
    public int? ListedFurnitureId { get; set; }

    /// <summary>The item's extra data as its furniture row held it (stuff data, serial).</summary>
    [Column("extra_data", TypeName = "longtext")]
    public string? ExtraData { get; set; }

    /// <summary>What a buyer pays.</summary>
    [Column("price")]
    public int Price { get; set; }

    /// <summary><see cref="STATE_OPEN"/> or <see cref="STATE_SOLD"/>; an open offer past <see cref="ExpiresAt"/> is expired.</summary>
    [Column("state")]
    public int State { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("buyer_player_id")]
    public int? BuyerPlayerEntityId { get; set; }

    [Column("sold_at")]
    public DateTime? SoldAt { get; set; }

    /// <summary>What the seller gets for the sale: the price less the selling fee.</summary>
    [Column("revenue")]
    public int Revenue { get; set; }

    /// <summary>Whether the seller has taken the sale's credits.</summary>
    [Column("credits_redeemed")]
    public bool CreditsRedeemed { get; set; }

    /// <summary>Whether the seller cleared this sold offer from their history (its credits are kept).</summary>
    [Column("history_cleared")]
    public bool HistoryCleared { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }

    [ForeignKey(nameof(FurnitureDefinitionEntityId))]
    public FurnitureDefinitionEntity? FurnitureDefinitionEntity { get; set; }
}
