using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Tracking;

/// <summary>
/// A step of a login the server saw (<c>ClientLoginSteps</c>), timed from the socket opening.
/// <see cref="Session"/> ties a login's steps together; the player is known from the ticket on.
/// </summary>
[Table("client_login_steps")]
[Index(nameof(CreatedAt))]
[Index(nameof(Session))]
public class ClientLoginStepEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("created_at")]
    public required DateTime CreatedAt { get; set; }

    [Column("session")]
    [MaxLength(64)]
    public required string Session { get; set; }

    [Column("player_id")]
    public int? PlayerId { get; set; }

    [Column("step")]
    [MaxLength(64)]
    public required string Step { get; set; }

    [Column("ms_since_connect")]
    public required int MsSinceConnect { get; set; }
}
