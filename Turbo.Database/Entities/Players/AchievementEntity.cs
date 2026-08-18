using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

[Table("achievements")]
[Index(nameof(Code), IsUnique = true)]
public class AchievementEntity : TurboEntity
{
    [Column("code")]
    public required string Code { get; set; }

    [Column("name")]
    public required string Name { get; set; }

    [Column("category")]
    public required string Category { get; set; }

    [Column("enabled")]
    [DefaultValue(true)]
    public required bool Enabled { get; set; }

    [InverseProperty("AchievementEntity")]
    public List<AchievementLevelEntity>? Levels { get; set; }

    [InverseProperty("AchievementEntity")]
    public List<PlayerAchievementEntity>? PlayerAchievements { get; set; }
}
