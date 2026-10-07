using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// One of Habbo's releases as a check found it: the revision its external variables named, and
/// the furniture data it served then, gzipped, so an import works from what was checked. A row is
/// one furniture data: Habbo serving the same data again only moves <see cref="CheckedAt"/>.
/// </summary>
[Table("habbo_releases")]
[Index(nameof(Domain), nameof(FurnitureDataHash), IsUnique = true)]
public class HabboReleaseEntity : TurboEntity
{
    [Column("domain")]
    [MaxLength(16)]
    public required string Domain { get; set; }

    [Column("revision")]
    [MaxLength(64)]
    public required string Revision { get; set; }

    [Column("furniture_data_hash")]
    [MaxLength(40)]
    public required string FurnitureDataHash { get; set; }

    [Column("furniture_data")]
    public required byte[] FurnitureData { get; set; }

    [Column("furniture_count")]
    public int FurnitureCount { get; set; }

    [Column("checked_at")]
    public DateTime CheckedAt { get; set; }

    [Column("imported_at")]
    public DateTime? ImportedAt { get; set; }
}
