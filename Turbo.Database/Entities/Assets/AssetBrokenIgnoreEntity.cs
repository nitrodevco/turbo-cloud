using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Assets;

/// <summary>
/// A broken asset staff chose to ignore, with why: the broken assets page shows it apart, and
/// it can be brought back.
/// </summary>
[Table("asset_broken_ignores")]
[Index(nameof(Source), nameof(Key), IsUnique = true)]
public class AssetBrokenIgnoreEntity : TurboEntity
{
    public const int SOURCE_MAX_LENGTH = 32;
    public const int KEY_MAX_LENGTH = 512;
    public const int NOTE_MAX_LENGTH = 512;

    /// <summary>Where it was found: <c>client</c>, <c>check</c>, <c>catalog</c>, <c>badge</c>.</summary>
    [Column("source")]
    [MaxLength(SOURCE_MAX_LENGTH)]
    public required string Source { get; set; }

    /// <summary>What it is within its source: an address, <c>furniture/chair</c>, a badge code.</summary>
    [Column("asset_key")]
    [MaxLength(KEY_MAX_LENGTH)]
    public required string Key { get; set; }

    [Column("note")]
    [MaxLength(NOTE_MAX_LENGTH)]
    public string Note { get; set; } = "";

    [Column("player_id")]
    public int PlayerId { get; set; }
}
