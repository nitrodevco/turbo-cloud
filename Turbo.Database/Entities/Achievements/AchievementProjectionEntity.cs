using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Achievements;

[Comment(
    "Each player's achievement score and levels as other players see them, and whether that still has to be published."
)]
[Table("achievement_projections")]
public sealed class AchievementProjectionEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int PlayerId { get; set; }
    public int Score { get; set; }

    /// <summary>Levels completed across every achievement, as the profile and the leaderboard show.</summary>
    public int CompletedLevels { get; set; }
    public bool PublicationPending { get; set; }

    /// <summary>
    /// The catalog revisions this player's retained progress was last evaluated against. While it
    /// matches, a login skips the badge cleanup and the award re-evaluation.
    /// </summary>
    [MaxLength(64)]
    public string ReconciledStamp { get; set; } = "";

    /// <summary>
    /// The last authoritative state value recorded as a fact for each source, as a JSON object of
    /// source to value. A state fact is only recorded again when its value moved, so a login that
    /// changes nothing writes nothing. A handful of entries per player at most.
    /// </summary>
    public string ObservedState { get; set; } = "{}";
}
