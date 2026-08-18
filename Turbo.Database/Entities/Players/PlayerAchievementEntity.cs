using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

[Table("player_achievements")]
[Index(nameof(PlayerEntityId), nameof(AchievementEntityId), IsUnique = true)]
public class PlayerAchievementEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("achievement_id")]
    public required int AchievementEntityId { get; set; }

    [Column("level")]
    [DefaultValue(0)]
    public required int Level { get; set; }

    [Column("progress")]
    [DefaultValue(0)]
    public required int Progress { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }

    [ForeignKey(nameof(AchievementEntityId))]
    public AchievementEntity? AchievementEntity { get; set; }
}
