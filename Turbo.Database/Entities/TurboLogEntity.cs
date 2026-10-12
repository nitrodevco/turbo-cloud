using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turbo.Database.Entities;

/// <summary>
/// A row of an append-only log (chat, room entries, private messages, command uses): a 64-bit id,
/// because a busy hotel writes these by the million and a 32-bit id runs out, and the time the
/// database created it. A log keeps no foreign keys to the rooms and players it names, so it
/// outlives them; that is what moderation reads it for.
/// </summary>
public class TurboLogEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("created_at")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }
}
