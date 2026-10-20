using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Tracking;

/// <summary>An event a client logged (<c>EventLog</c>), as it sent it.</summary>
[Table("client_event_logs")]
[Index(nameof(CreatedAt))]
[Index(nameof(PlayerId), nameof(CreatedAt))]
[Index(nameof(RoomId), nameof(CreatedAt))]
[Index(nameof(Category), nameof(CreatedAt))]
public class ClientEventLogEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("created_at")]
    public required DateTime CreatedAt { get; set; }

    [Column("player_id")]
    public required int PlayerId { get; set; }

    [Column("room_id")]
    public int? RoomId { get; set; }

    [Column("category")]
    [MaxLength(64)]
    public required string Category { get; set; }

    [Column("type")]
    [MaxLength(128)]
    public required string Type { get; set; }

    [Column("action")]
    [MaxLength(128)]
    public required string Action { get; set; }

    [Column("extra_string")]
    [MaxLength(255)]
    public required string ExtraString { get; set; }

    [Column("extra_int")]
    public required int ExtraInt { get; set; }
}
