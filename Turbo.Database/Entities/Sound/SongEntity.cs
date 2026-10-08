using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Sound;

/// <summary>
/// A trax song: the track a song disk plays in a jukebox. Staff add them in the admin panel; the
/// song directory grain owns the table, so every change goes through it.
/// </summary>
[Table("songs")]
[Index(nameof(Code), IsUnique = true)]
public class SongEntity : TurboEntity
{
    public const int CODE_MAX_LENGTH = 64;
    public const int NAME_MAX_LENGTH = 100;
    public const int AUTHOR_MAX_LENGTH = 50;

    /// <summary>The catalog code an official song can be sold under instead of its id; null for none.</summary>
    [Column("code")]
    [MaxLength(CODE_MAX_LENGTH)]
    public string? Code { get; set; }

    [Column("name")]
    [MaxLength(NAME_MAX_LENGTH)]
    public required string Name { get; set; }

    [Column("author")]
    [MaxLength(AUTHOR_MAX_LENGTH)]
    public required string Author { get; set; }

    /// <summary>The trax track in the client's format, read by the client only.</summary>
    [Column("track", TypeName = "longtext")]
    public required string Track { get; set; }

    [Column("length_seconds")]
    public int LengthSeconds { get; set; }

    /// <summary>Added by staff for the catalog to sell on disks.</summary>
    [Column("is_official")]
    public bool IsOfficial { get; set; }
}
