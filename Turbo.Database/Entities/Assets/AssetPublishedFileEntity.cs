using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Assets;

/// <summary>
/// A file a publish target holds, as it was last sent there: a publish sends only what is new or
/// whose hash changed, and one that stops part way resumes from what is recorded.
/// </summary>
[Table("asset_published_files")]
[Index(nameof(TargetEntityId), nameof(Path), IsUnique = true)]
public class AssetPublishedFileEntity : TurboEntity
{
    [Column("target_id")]
    public required int TargetEntityId { get; set; }

    /// <summary>Its path under the target's folder: <c>bundled/furniture/chair.nitro</c>.</summary>
    [Column("path")]
    [MaxLength(255)]
    public required string Path { get; set; }

    [Column("hash")]
    [MaxLength(40)]
    public required string Hash { get; set; }

    [ForeignKey(nameof(TargetEntityId))]
    public AssetPublishTargetEntity? Target { get; set; }
}
