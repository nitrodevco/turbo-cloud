using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Guilds.Enums;

namespace Turbo.Database.Entities.Guilds;

/// <summary>
/// A colour the badge editor may pick, from one of its three palettes. As with a badge part,
/// <see cref="ColorId"/> is the number written into a badge code and must stay put; the hex
/// behind it may be retuned freely, and every badge using it repaints.
/// </summary>
[Table("guild_colors")]
[Index(nameof(Slot), nameof(ColorId), IsUnique = true)]
public class GuildColorEntity : TurboEntity
{
    public const int COLOR_MAX_LENGTH = 6;

    [Column("slot")]
    [DefaultValue(GuildColorSlotType.Badge)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required GuildColorSlotType Slot { get; set; }

    /// <summary>The number written into a badge code, for the badge palette.</summary>
    [Column("color_id")]
    public required int ColorId { get; set; }

    /// <summary>Six hex digits, no <c>#</c>.</summary>
    [Column("color")]
    [MaxLength(COLOR_MAX_LENGTH)]
    public required string Color { get; set; }
}
