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

    /// <summary>
    /// The catalog revisions this player's retained progress was last evaluated against. While it
    /// matches, a login skips the badge cleanup and the award re-evaluation.
    /// </summary>
    [MaxLength(64)]
    public string ReconciledStamp { get; set; } = "";
}
