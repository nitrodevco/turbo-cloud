using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Attributes;

namespace Turbo.Database.Entities.Players;

[TurboEntity]
[Table("player_badges")]
[Index(nameof(PlayerEntityId), nameof(BadgeCode), IsUnique = true)]
public class PlayerBadgeEntity : Entity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("badge_code")]
    public required string BadgeCode { get; set; }

    [Column("slot_id")]
    [DefaultValue(0)]
    public int? SlotId { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }
}
