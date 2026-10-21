using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Tracking;

/// <summary>
/// The chat lag a client noticed since its last report (<c>LagWarningReport</c>): how many of its
/// own chat lines came back later than the client allows.
/// </summary>
[Table("client_lag_warnings")]
[Index(nameof(CreatedAt))]
[Index(nameof(PlayerId), nameof(CreatedAt))]
[Index(nameof(RoomId), nameof(CreatedAt))]
public class ClientLagWarningEntity
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

    [Column("warning_count")]
    public required int WarningCount { get; set; }
}
