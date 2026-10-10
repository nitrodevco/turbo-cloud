using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Database.Entities.Assets;

/// <summary>
/// A <c>.nitro</c> bundle the hotel keeps, by its kind and name: the file itself is on disk
/// (<c>bundled/&lt;kind&gt;/&lt;name&gt;.nitro</c> under the asset directory), and this row is what
/// is known of it without opening it. A library that could not be fetched or converted keeps why
/// in <see cref="Error"/> and has no file; it is tried again only when that may pass
/// (<see cref="Retry"/>) or its revision changes.
/// </summary>
[Table("asset_bundles")]
[Index(nameof(Kind), nameof(Name), IsUnique = true)]
public class AssetBundleEntity : TurboEntity
{
    public const int NAME_MAX_LENGTH = 128;
    public const int ERROR_MAX_LENGTH = 512;
    public const int IDS_MAX_LENGTH = 1024;

    [Column("kind")]
    public required AssetBundleKind Kind { get; set; }

    [Column("name")]
    [MaxLength(NAME_MAX_LENGTH)]
    public required string Name { get; set; }

    /// <summary>
    /// The revision the file was taken at: furnidata's for a furniture, the effect map's for an
    /// effect, the client's (<c>flash-assets-&lt;revision&gt;</c>) for a pet. Null for an upload.
    /// </summary>
    [Column("revision")]
    [MaxLength(64)]
    public string? Revision { get; set; }

    [Column("source")]
    public required AssetBundleSource Source { get; set; }

    /// <summary>The bundle's SHA-1 in lowercase hex; null while it has no file.</summary>
    [Column("hash")]
    [MaxLength(40)]
    public string? Hash { get; set; }

    [Column("size")]
    public long Size { get; set; }

    /// <summary>
    /// The ids that load it, comma separated: the effects sharing an effect's library, a pet's
    /// type. Null for a furniture, which is found by its name.
    /// </summary>
    [Column("ids")]
    [MaxLength(IDS_MAX_LENGTH)]
    public string? Ids { get; set; }

    [Column("error")]
    [MaxLength(ERROR_MAX_LENGTH)]
    public string? Error { get; set; }

    /// <summary>Whether a failed library is tried again on the next sync: a download that failed may work then.</summary>
    [Column("retry")]
    public bool Retry { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
