using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Turbo.Database.Entities.Navigator;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Database.Entities.Room;

[Table("rooms")]
public class RoomEntity : TurboEntity
{
    public const WiredPermissionFlags DEFAULT_WIRED_PERMISSION_MASK = WiredPermissionFlags.Rights;
    public const string DEFAULT_WIRED_TIMEZONE = "UTC";
    public const int WIRED_TIMEZONE_MAX_LENGTH = 64;
    public const int TAGS_MAX_LENGTH = 128;

    [Column("name")]
    public required string Name { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("door_mode")]
    [DefaultValue(RoomDoorModeType.Open)] // DoorModeType.Open
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required RoomDoorModeType DoorMode { get; set; }

    [Column("password")]
    public string? Password { get; set; }

    [Column("model_id")]
    public required int RoomModelEntityId { get; set; }

    [Column("category_id")]
    public int? NavigatorCategoryEntityId { get; set; }

    [Column("users_now")]
    [DefaultValue(0)]
    public required int UsersNow { get; set; }

    [Column("players_max")]
    [DefaultValue(25)]
    public required int PlayersMax { get; set; }

    [Column("paint_wall")]
    public string? PaintWall { get; set; }

    [Column("paint_floor")]
    public string? PaintFloor { get; set; }

    [Column("paint_landscape")]
    public string? PaintLandscape { get; set; }

    [Column("wall_height")]
    [DefaultValue(-1)]
    public required int WallHeight { get; set; }

    [Column("hide_walls")]
    [DefaultValue(false)]
    public required bool HideWalls { get; set; }

    [Column("thickness_wall")]
    [DefaultValue(RoomThicknessType.Normal)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required RoomThicknessType ThicknessWall { get; set; }

    [Column("thickness_floor")]
    [DefaultValue(RoomThicknessType.Normal)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required RoomThicknessType ThicknessFloor { get; set; }

    [Column("allow_blocking")]
    [DefaultValue(false)]
    public required bool AllowBlocking { get; set; }

    [Column("allow_pets")]
    [DefaultValue(false)]
    public required bool AllowPets { get; set; }

    [Column("allow_pets_eat")]
    [DefaultValue(false)]
    public required bool AllowPetsEat { get; set; }

    [Column("trade_type")]
    [DefaultValue(RoomTradeModeType.Disabled)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required RoomTradeModeType TradeType { get; set; }

    [Column("mute_type")]
    [DefaultValue(ModSettingType.Owner)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required ModSettingType MuteType { get; set; }

    [Column("kick_type")]
    [DefaultValue(ModSettingType.Owner)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required ModSettingType KickType { get; set; }

    [Column("ban_type")]
    [DefaultValue(ModSettingType.Owner)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required ModSettingType BanType { get; set; }

    [Column("chat_flood_type")]
    [DefaultValue(ChatFloodSensitivityType.Minimal)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required ChatFloodSensitivityType ChatFloodType { get; set; }

    [Column("leave_on_door_tile")]
    [DefaultValue(false)]
    public bool LeaveOnDoorTile { get; set; }

    [Column("idle_sleep_enabled")]
    [DefaultValue(false)]
    public bool IdleSleepEnabled { get; set; }

    [Column("idle_sleep_timeout_seconds")]
    [DefaultValue(0)]
    public int IdleSleepTimeoutSeconds { get; set; }

    [Column("idle_autokick_enabled")]
    [DefaultValue(false)]
    public bool IdleAutokickEnabled { get; set; }

    [Column("idle_autokick_timeout_seconds")]
    [DefaultValue(0)]
    public int IdleAutokickTimeoutSeconds { get; set; }

    [Column("mute_all_pets")]
    [DefaultValue(false)]
    public bool MuteAllPets { get; set; }

    [Column("score")]
    [DefaultValue(0)]
    public int Score { get; set; }

    /// <summary>Comma-separated room tags shown and searched in the navigator.</summary>
    [Column("tags")]
    [MaxLength(TAGS_MAX_LENGTH)]
    public string? Tags { get; set; }

    [Column("staff_pick")]
    [DefaultValue(false)]
    public bool StaffPick { get; set; }

    [Column("hidden_by_bc")]
    [DefaultValue(false)]
    public bool HiddenByBc { get; set; }

    [Column("wired_modify_permission_mask")]
    [DefaultValue(DEFAULT_WIRED_PERMISSION_MASK)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public WiredPermissionFlags WiredModifyPermissionMask { get; set; } =
        DEFAULT_WIRED_PERMISSION_MASK;

    [Column("wired_read_permission_mask")]
    [DefaultValue(DEFAULT_WIRED_PERMISSION_MASK)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public WiredPermissionFlags WiredReadPermissionMask { get; set; } =
        DEFAULT_WIRED_PERMISSION_MASK;

    [Column("wired_timezone")]
    [MaxLength(WIRED_TIMEZONE_MAX_LENGTH)]
    [DefaultValue(DEFAULT_WIRED_TIMEZONE)]
    public string WiredTimezone { get; set; } = DEFAULT_WIRED_TIMEZONE;

    [Column("last_active")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime LastActive { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }

    [ForeignKey(nameof(RoomModelEntityId))]
    public required RoomModelEntity RoomModelEntity { get; set; }

    [ForeignKey(nameof(NavigatorCategoryEntityId))]
    public NavigatorFlatCategoryEntity? NavigatorFlatCategoryEntity { get; set; }

    [InverseProperty("RoomEntity")]
    public List<RoomBanEntity>? RoomBans { get; set; }

    [InverseProperty("RoomEntity")]
    public List<RoomMuteEntity>? RoomMutes { get; set; }

    [InverseProperty("RoomEntity")]
    public List<RoomRightEntity>? RoomRights { get; set; }

    [InverseProperty("RoomEntity")]
    public List<RoomChatlogEntity>? RoomChats { get; set; }

    [InverseProperty("RoomEntity")]
    public List<RoomRatingEntity>? RoomRatings { get; set; }

    [InverseProperty("RoomEntity")]
    public List<RoomEventEntity>? RoomEvents { get; set; }
    public List<RoomFilterWordEntity>? RoomFilterWords { get; set; }
}
