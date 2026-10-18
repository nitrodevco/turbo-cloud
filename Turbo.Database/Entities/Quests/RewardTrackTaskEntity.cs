using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Quests;

/// <summary>
/// A reward track task: <see cref="TaskId"/> names its texts, <see cref="ActionType"/> what it
/// counts (<c>RewardTrackActionTypes</c>); its levels give their points as the count reaches them.
/// </summary>
[Table("reward_track_tasks")]
[Index(nameof(RewardTrackEntityId), nameof(TaskId), IsUnique = true)]
public class RewardTrackTaskEntity : TurboEntity
{
    [Column("reward_track_id")]
    public int RewardTrackEntityId { get; set; }

    [Column("task_id")]
    [StringLength(RewardTrackEntity.ID_MAX_LENGTH)]
    public required string TaskId { get; set; }

    [Column("action_type")]
    [StringLength(RewardTrackEntity.ID_MAX_LENGTH)]
    public required string ActionType { get; set; }

    [Column("parameter")]
    [StringLength(255)]
    public string Parameter { get; set; } = "";

    [Column("premium")]
    [DefaultValue(false)]
    public bool Premium { get; set; }

    [Column("sort_order")]
    [DefaultValue(0)]
    public int SortOrder { get; set; }

    public List<RewardTrackTaskLevelEntity> Levels { get; set; } = [];

    [ForeignKey(nameof(RewardTrackEntityId))]
    public RewardTrackEntity? RewardTrackEntity { get; set; }
}
