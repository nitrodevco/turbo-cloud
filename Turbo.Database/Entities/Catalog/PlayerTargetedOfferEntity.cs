using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Catalog;

/// <summary>
/// Where a player is with one targeted offer: where they left its window and how many they
/// bought. The offers themselves are configuration (<c>Turbo:TargetedOffers</c>), so
/// <see cref="OfferId"/> names one there.
/// </summary>
[Table("player_targeted_offers")]
[Index(nameof(PlayerEntityId), nameof(OfferId), IsUnique = true)]
public class PlayerTargetedOfferEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("offer_id")]
    public required int OfferId { get; set; }

    [Column("tracking_state")]
    public required int TrackingState { get; set; }

    [Column("purchased")]
    public required int Purchased { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }
}
