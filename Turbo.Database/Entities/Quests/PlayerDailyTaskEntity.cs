using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Quests.Enums;

namespace Turbo.Database.Entities.Quests;

/// <summary>
/// A daily task given to a player for one day. The id is the task id the client holds.
/// <see cref="Counted"/> keeps what an explore task has already counted (comma-separated room
/// ids), so a room counts once.
/// </summary>
[Comment("A daily task given to a player for one day.")]
[Table("player_daily_tasks")]
// One of each task a day: the grain gives only definitions the player hasn't had that day.
[Index(nameof(PlayerEntityId), nameof(DayStartsAt), nameof(DefinitionEntityId), IsUnique = true)]
public class PlayerDailyTaskEntity : TurboEntity
{
    /// <summary>The most rooms an explore task counts.</summary>
    public const int COUNTED_MAX = 100;

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("definition_id")]
    public required int DefinitionEntityId { get; set; }

    /// <summary>The start of the task's day (the configured reset time, UTC).</summary>
    [Column("day_starts_at")]
    public required DateTime DayStartsAt { get; set; }

    [Column("repeats")]
    [DefaultValue(0)]
    public int Repeats { get; set; }

    /// <summary>An explore task's rooms already counted today, so a room counts once.</summary>
    [Column("counted")]
    public List<string> Counted { get; set; } = [];

    [Column("status")]
    [DefaultValue(DailyTaskStatus.Active)]
    public DailyTaskStatus Status { get; set; }

    [Column("completed_at")]
    public DateTime? CompletedAt { get; set; }

    [Column("claimed_at")]
    public DateTime? ClaimedAt { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }

    [ForeignKey(nameof(DefinitionEntityId))]
    public DailyTaskDefinitionEntity? Definition { get; set; }
}
