using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

[Comment("The badges each player owns, and which slot each is worn in.")]
[Table("player_badges")]
[Index(nameof(PlayerEntityId), nameof(BadgeCode), IsUnique = true)]
// Owner counts group by code alone, which the unique index above (player first) cannot serve.
[Index(nameof(BadgeCode))]
public class PlayerBadgeEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("badge_code")]
    public required string BadgeCode { get; set; }

    /// <summary>The profile slot it is worn in, from 1; null when it is not worn.</summary>
    [Column("slot_id")]
    public int? SlotId { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }
}
