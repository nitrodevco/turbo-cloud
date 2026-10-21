using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// One version of Habbo's external variables as a check found it, gzipped, by its hash: its
/// <c>key=value</c> lines as served. Habbo changes its switches (seasonal currencies, campaign
/// pages, features) apart from its furniture and texts, so they are kept apart from both.
/// </summary>
[Table("habbo_variable_versions")]
[Index(nameof(Domain), nameof(Hash), IsUnique = true)]
public class HabboVariableVersionEntity : TurboEntity, IHabboVersionEntity
{
    [Column("domain")]
    [MaxLength(16)]
    public required string Domain { get; set; }

    [Column("hash")]
    [MaxLength(40)]
    public required string Hash { get; set; }

    [Column("content")]
    public required byte[] Content { get; set; }

    [Column("variable_count")]
    public int VariableCount { get; set; }

    [Column("checked_at")]
    public DateTime CheckedAt { get; set; }

    /// <summary>When any of its keys was last taken in.</summary>
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
