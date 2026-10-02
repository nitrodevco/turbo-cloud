using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turbo.Database.Entities.Room;

/// <summary>
/// One use of a chat command by an executor who holds <c>command.log</c>, so an operator logs
/// staff and nobody else. The ids are plain columns, not foreign keys: the log is an audit trail
/// and outlives the room it was typed in.
/// </summary>
[Table("command_logs")]
public class CommandLogEntity : TurboEntity
{
    public const int COMMAND_MAX_LENGTH = 32;
    public const int ARGUMENTS_MAX_LENGTH = 255;
    public const int OUTCOME_MAX_LENGTH = 16;

    [Column("room_id")]
    public required int RoomEntityId { get; set; }

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("command")]
    [StringLength(COMMAND_MAX_LENGTH)]
    public required string Command { get; set; }

    [Column("arguments")]
    [StringLength(ARGUMENTS_MAX_LENGTH)]
    public required string Arguments { get; set; }

    [Column("outcome")]
    [StringLength(OUTCOME_MAX_LENGTH)]
    public required string Outcome { get; set; }

    [Column("execution_id")]
    public Guid? ExecutionId { get; set; }

    [Column("parent_execution_id")]
    public Guid? ParentExecutionId { get; set; }

    [Column("confirmation_id")]
    public Guid? ConfirmationId { get; set; }

    [Column("source")]
    [StringLength(16)]
    public string? Source { get; set; }

    [Column("resolved_audience_json", TypeName = "json")]
    public string? ResolvedAudienceJson { get; set; }

    [Column("batch_target_results_json", TypeName = "json")]
    public string? BatchTargetResultsJson { get; set; }
}
