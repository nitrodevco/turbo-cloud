using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turbo.Database.Entities.Scheduling;

/// <summary>
/// The last run of a plugin's scheduled task, by the task's key: what the admin panel shows, and
/// what the scheduler counts the next run from after a restart (docs/plugin-scheduler.md). A
/// task's row stays when its plugin is removed, so a reinstall picks up its schedule where it was.
/// </summary>
[Table("scheduled_task_runs")]
public class ScheduledTaskRunEntity
{
    public const int KEY_MAX_LENGTH = 128;

    public const int ERROR_MAX_LENGTH = 1000;

    [Key]
    [Column("task_key")]
    [MaxLength(KEY_MAX_LENGTH)]
    public required string Key { get; set; }

    /// <summary>When the run was due (the scheduler counts from this, not from when it started).</summary>
    [Column("scheduled_for")]
    public DateTime ScheduledForUtc { get; set; }

    [Column("started_at")]
    public DateTime StartedAtUtc { get; set; }

    [Column("duration_ms")]
    public long DurationMs { get; set; }

    /// <summary>Succeeded, Failed, Cancelled or Lost.</summary>
    [Column("outcome")]
    [MaxLength(16)]
    public required string Outcome { get; set; }

    [Column("error")]
    [MaxLength(ERROR_MAX_LENGTH)]
    public string? Error { get; set; }

    [Column("manual")]
    public bool Manual { get; set; }
}
