using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// One version of Habbo's external texts as a check found it, gzipped, by its hash. Habbo
/// changes its texts apart from its furniture, so they are kept apart from its releases.
/// </summary>
[Table("habbo_text_versions")]
[Index(nameof(Domain), nameof(Hash), IsUnique = true)]
public class HabboTextVersionEntity : TurboEntity, IHabboVersionEntity
{
    [Column("domain")]
    [MaxLength(16)]
    public required string Domain { get; set; }

    [Column("hash")]
    [MaxLength(40)]
    public required string Hash { get; set; }

    [Column("content")]
    public required byte[] Content { get; set; }

    [Column("text_count")]
    public int TextCount { get; set; }

    [Column("checked_at")]
    public DateTime CheckedAt { get; set; }

    [Column("imported_at")]
    public DateTime? ImportedAt { get; set; }

    /// <summary>
    /// The hash Habbo's <c>/gamedata/hashes</c> listed for the file when this version was last
    /// found; null when it could not be read.
    /// </summary>
    [Column("habbo_hash")]
    [MaxLength(64)]
    public string? HabboHash { get; set; }
}
