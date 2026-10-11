using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Achievements;

[Comment(
    "The hotel's achievements: each one's levels, badge, what counts toward it and its rewards."
)]
[Table("achievement_definitions")]
[PrimaryKey(nameof(AchievementId), nameof(Revision))]
public sealed class AchievementDefinitionEntity
{
    public int AchievementId { get; set; }
    public int Revision { get; set; }

    [Column(TypeName = "text")]
    [MaxLength(int.MaxValue)]
    public required string DefinitionJson { get; set; }
}
