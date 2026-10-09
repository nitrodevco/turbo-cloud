using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Quests;

/// <summary>
/// A player's progress on a quest: accepted (the one they are doing), the steps done, and when
/// it was completed. A quest with no row was never started.
/// </summary>
[Table("player_quests")]
[Index(nameof(PlayerEntityId), nameof(QuestEntityId), IsUnique = true)]
public class PlayerQuestEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("quest_id")]
    public required int QuestEntityId { get; set; }

    [Column("accepted")]
    [DefaultValue(false)]
    public bool Accepted { get; set; }

    [Column("progress")]
    [DefaultValue(0)]
    public int Progress { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [ForeignKey(nameof(QuestEntityId))]
    public QuestEntity? Quest { get; set; }
}
