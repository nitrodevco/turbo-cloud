using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// One version of Habbo's figure data (<c>/gamedata/figuredata</c>, XML) as a check found it,
/// gzipped, by its hash. Habbo changes it apart from its other files, so it is kept apart.
/// </summary>
[Table("habbo_figure_versions")]
[Index(nameof(Domain), nameof(Hash), IsUnique = true)]
public class HabboFigureVersionEntity : TurboEntity
{
    [Column("domain")]
    [MaxLength(16)]
    public required string Domain { get; set; }

    [Column("hash")]
    [MaxLength(40)]
    public required string Hash { get; set; }

    [Column("content")]
    public required byte[] Content { get; set; }

    [Column("set_count")]
    public int SetCount { get; set; }

    [Column("color_count")]
    public int ColorCount { get; set; }

    [Column("checked_at")]
    public DateTime CheckedAt { get; set; }

    [Column("imported_at")]
    public DateTime? ImportedAt { get; set; }
}
