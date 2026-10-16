using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// A free product a player has claimed (<c>ClaimProduct</c>): one row per claim id, so each can be
/// taken once. The claims themselves are configuration, so <see cref="ClaimId"/> names one there.
/// </summary>
[Table("player_product_claims")]
[Index(nameof(PlayerEntityId), nameof(ClaimId), IsUnique = true)]
public class PlayerProductClaimEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("claim_id")]
    [MaxLength(64)]
    public required string ClaimId { get; set; }

    /// <summary>The choice the player made (the client's option index), -1 when the claim offers none.</summary>
    [Column("choice")]
    public required int Choice { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
