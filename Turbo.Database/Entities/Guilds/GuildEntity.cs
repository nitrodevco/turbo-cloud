using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Guilds.Enums;

namespace Turbo.Database.Entities.Guilds;

/// <summary>
/// A group. The homeroom lives here, on the group, rather than as a column on the room: a room
/// has at most one group and a group exactly one room, so either side could hold it, but the
/// group is the row that is created, edited and deleted as a unit, and the homeroom never
/// changes once chosen. The unique index is what enforces "at most one".
/// </summary>
[Table("guilds")]
[Index(nameof(RoomEntityId), IsUnique = true)]
[Index(nameof(PlayerEntityId))]
[Index(nameof(Name))]
public class GuildEntity : TurboEntity
{
    public const int NAME_MAX_LENGTH = 30;
    public const int DESCRIPTION_MAX_LENGTH = 255;
    public const int BADGE_CODE_MAX_LENGTH = 64;

    [Column("name")]
    [MaxLength(NAME_MAX_LENGTH)]
    public required string Name { get; set; }

    [Column("description")]
    [MaxLength(DESCRIPTION_MAX_LENGTH)]
    public required string Description { get; set; }

    /// <summary>Built by <c>GuildBadgeCodes</c> from the parts the editor sent.</summary>
    [Column("badge_code")]
    [MaxLength(BADGE_CODE_MAX_LENGTH)]
    public required string BadgeCode { get; set; }

    /// <summary>
    /// The group's colours as ids into <c>guild_colors</c>, not as hex. The id is what the edit
    /// window preselects in its palette, and keeping the id means retuning a palette entry
    /// repaints every group using it instead of leaving a stale copy behind.
    /// </summary>
    [Column("primary_color_id")]
    public required int PrimaryColorId { get; set; }

    [Column("secondary_color_id")]
    public required int SecondaryColorId { get; set; }

    [Column("guild_type")]
    [DefaultValue(GuildType.Regular)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required GuildType GuildType { get; set; }

    [Column("rights_level")]
    [DefaultValue(GuildRightsLevel.Admins)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required GuildRightsLevel RightsLevel { get; set; }

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("room_id")]
    public required int RoomEntityId { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }

    [ForeignKey(nameof(RoomEntityId))]
    public RoomEntity? RoomEntity { get; set; }
}
