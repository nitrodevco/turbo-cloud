using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

[Table("player_badges")]
[Index(nameof(PlayerEntityId), nameof(BadgeCode), IsUnique = true)]
// Owner counts group by code alone, which the unique index above (player first) cannot serve.
[Index(nameof(BadgeCode))]
public class PlayerBadgeEntity : TurboEntity
{
    /// <summary>Independent ownership, including all badges that predate achievement entitlements.</summary>
    [Column("manual_grant")]
    [DefaultValue(true)]
    public bool ManualGrant { get; set; } = true;

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
