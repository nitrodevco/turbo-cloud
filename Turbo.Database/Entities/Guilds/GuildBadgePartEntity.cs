using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Guilds.Enums;

namespace Turbo.Database.Entities.Guilds;

/// <summary>
/// A part the badge editor may pick. <see cref="PartId"/> is not the row id: it is the number
/// that goes into a badge code, and it has to stay put for ever — change it and every badge
/// already built from that part draws something else. That is why it is its own column, unique
/// per part type, rather than the primary key doing double duty.
/// </summary>
[Table("guild_badge_parts")]
[Index(nameof(PartType), nameof(PartId), IsUnique = true)]
public class GuildBadgePartEntity : TurboEntity
{
    public const int FILE_NAME_MAX_LENGTH = 64;

    [Column("part_type")]
    [DefaultValue(GuildBadgePartType.Base)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required GuildBadgePartType PartType { get; set; }

    /// <summary>The number written into a badge code. Never reuse one for a different picture.</summary>
    [Column("part_id")]
    public required int PartId { get; set; }

    /// <summary>Loaded by the client as <c>badgepart_&lt;name&gt;.png</c>; tinted with the layer's colour.</summary>
    [Column("file_name")]
    [MaxLength(FILE_NAME_MAX_LENGTH)]
    public required string FileName { get; set; }

    /// <summary>The untinted overlay laid over the part. Empty when it has none.</summary>
    [Column("mask_file_name")]
    [MaxLength(FILE_NAME_MAX_LENGTH)]
    public required string MaskFileName { get; set; }
}
