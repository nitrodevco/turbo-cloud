using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Assets;

/// <summary>
/// An asset players' clients could not load, by its address, as the client errors plugin told of
/// it ("Invalid response 404 for ..."): kept so the broken assets page lists it until it is fixed
/// or ignored, across restarts.
/// </summary>
[Table("asset_broken_reports")]
[Index(nameof(Url), IsUnique = true)]
public class AssetBrokenReportEntity : TurboEntity
{
    public const int URL_MAX_LENGTH = 512;

    [Column("url")]
    [MaxLength(URL_MAX_LENGTH)]
    public required string Url { get; set; }

    /// <summary>The HTTP status the client was answered with.</summary>
    [Column("status")]
    public int Status { get; set; }

    /// <summary>How many times it was told of (each new or returning client error).</summary>
    [Column("reports")]
    public int Reports { get; set; }

    /// <summary>Where the client error that told of it last is read.</summary>
    [Column("issue_url")]
    [MaxLength(URL_MAX_LENGTH)]
    public string? IssueUrl { get; set; }

    [Column("last_seen_at")]
    public DateTime LastSeenAt { get; set; }
}
