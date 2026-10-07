using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// One version of Habbo's product data as a check found it, gzipped, by its hash. Habbo changes it
/// apart from its furniture and texts, so it is kept apart from them.
/// </summary>
[Table("habbo_product_versions")]
[Index(nameof(Domain), nameof(Hash), IsUnique = true)]
public class HabboProductVersionEntity : TurboEntity
{
    [Column("domain")]
    [MaxLength(16)]
    public required string Domain { get; set; }

    [Column("hash")]
    [MaxLength(40)]
    public required string Hash { get; set; }

    [Column("content")]
    public required byte[] Content { get; set; }

    [Column("product_count")]
    public int ProductCount { get; set; }

    [Column("checked_at")]
    public DateTime CheckedAt { get; set; }

    [Column("imported_at")]
    public DateTime? ImportedAt { get; set; }
}
