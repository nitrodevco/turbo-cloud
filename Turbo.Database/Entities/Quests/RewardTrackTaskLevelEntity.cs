using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Quests;

/// <summary>A task's level: the count that reaches it and the points it gives.</summary>
[Table("reward_track_task_levels")]
[Index(nameof(RewardTrackTaskEntityId), nameof(RequiredCount), IsUnique = true)]
public class RewardTrackTaskLevelEntity : TurboEntity
{
    [Column("reward_track_task_id")]
    public int RewardTrackTaskEntityId { get; set; }

    [Column("required_count")]
    public int RequiredCount { get; set; }

    [Column("points")]
    [DefaultValue(0)]
    public int Points { get; set; }

    [Column("premium")]
    [DefaultValue(false)]
    public bool Premium { get; set; }

    [ForeignKey(nameof(RewardTrackTaskEntityId))]
    public RewardTrackTaskEntity? RewardTrackTaskEntity { get; set; }
}
