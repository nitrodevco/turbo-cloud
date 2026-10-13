using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Catalog;

/// <summary>
/// A player's marketplace offer tokens: each offer posted uses one when the hotel sells them in
/// batches (marketplace TokenBatchSize above 0).
/// </summary>
[Table("marketplace_tokens")]
[Index(nameof(PlayerEntityId), IsUnique = true)]
public class MarketplaceTokenEntity : TurboEntity
{
    [Column("player_id")]
    public int PlayerEntityId { get; set; }

    [Column("tokens")]
    public int Tokens { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
