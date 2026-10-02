using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turbo.Database.Entities.Achievements;

[Table("achievement_projections")]
public sealed class AchievementProjectionEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int PlayerId { get; set; }
    public int Score { get; set; }
    public int EarnedLevels { get; set; }
    public bool PublicationPending { get; set; }
}
