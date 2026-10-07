using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// What one of Habbo's furniture asset files said, by its asset name (a classname without its
/// <c>*N</c> colour) and the revision furnidata gave it: the file is read once per revision.
/// A file that could not be fetched or read keeps why in <see cref="Error"/>; it is tried again
/// only when that may pass (<see cref="Retry"/>).
/// </summary>
[Table("habbo_furniture_assets")]
[Index(nameof(AssetName), nameof(Revision), IsUnique = true)]
public class HabboFurnitureAssetEntity : TurboEntity
{
    public const int ERROR_MAX_LENGTH = 512;

    [Column("asset_name")]
    [MaxLength(128)]
    public required string AssetName { get; set; }

    [Column("revision")]
    public required int Revision { get; set; }

    /// <summary>The states it toggles through, from its animations.</summary>
    [Column("states")]
    public int States { get; set; }

    [Column("logic_type")]
    [MaxLength(64)]
    public string? LogicType { get; set; }

    [Column("visualization_type")]
    [MaxLength(64)]
    public string? VisualizationType { get; set; }

    /// <summary>Everything read from the file (<c>FurnitureAssetInfo</c>), as JSON.</summary>
    [Column("info", TypeName = "longtext")]
    public string? Info { get; set; }

    [Column("error")]
    [MaxLength(ERROR_MAX_LENGTH)]
    public string? Error { get; set; }

    /// <summary>
    /// Whether a failed file is tried again: a download that failed may work next time. A file
    /// Habbo has no file for, or one that doesn't read, fails the same way until its revision
    /// changes, so it is settled.
    /// </summary>
    [Column("retry")]
    public bool Retry { get; set; }
}
