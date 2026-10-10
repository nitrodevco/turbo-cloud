using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turbo.Database.Entities.Assets;

/// <summary>A publish, for its target's history: who started it, what it sent and removed, and how it ended.</summary>
[Table("asset_publishes")]
public class AssetPublishEntity : TurboEntity
{
    public const int ERROR_MAX_LENGTH = 512;

    [Column("target_id")]
    public required int TargetEntityId { get; set; }

    [Column("player_id")]
    public int PlayerId { get; set; }

    /// <summary>Only counted what would be sent; nothing was.</summary>
    [Column("dry_run")]
    public bool DryRun { get; set; }

    [Column("uploaded")]
    public int Uploaded { get; set; }

    [Column("skipped")]
    public int Skipped { get; set; }

    [Column("deleted")]
    public int Deleted { get; set; }

    [Column("bytes")]
    public long Bytes { get; set; }

    [Column("error")]
    [MaxLength(ERROR_MAX_LENGTH)]
    public string? Error { get; set; }

    /// <summary>Null while it runs, or when the server stopped under it.</summary>
    [Column("finished_at")]
    public DateTime? FinishedAt { get; set; }

    [ForeignKey(nameof(TargetEntityId))]
    public AssetPublishTargetEntity? Target { get; set; }
}
