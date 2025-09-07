using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Turbo.Contracts.Enums.Rooms;
using Turbo.Database.Attributes;
using Turbo.Database.Entities.Navigator;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Room;

[TurboEntity]
[Table("rooms")]
public class RoomEntity : Entity
{
    [Column("name")]
    public required string Name { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("state")]
    [DefaultValue(RoomStateType.Open)] // RoomStateType.Open
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required RoomStateType RoomState { get; set; }

    [Column("password")]
    public string? Password { get; set; }

    [Column("model_id")]
    public required int RoomModelEntityId { get; set; }

    [Column("category_id")]
    public int? NavigatorCategoryEntityId { get; set; }

    [Column("users_now")]
    [DefaultValue(0)]
    public required int UsersNow { get; set; }

    [Column("users_max")]
    [DefaultValue(25)]
    public required int UsersMax { get; set; }

    [Column("paint_wall")]
    [DefaultValue(0.0d)]
    public required double PaintWall { get; set; }

    [Column("paint_floor")]
    [DefaultValue(0.0d)]
    public required double PaintFloor { get; set; }

    [Column("paint_landscape")]
    [DefaultValue(0.0d)]
    public required double PaintLandscape { get; set; }

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

    [Column("allow_walk_through")]
    [DefaultValue(true)]
    public required bool AllowWalkThrough { get; set; }

    [Column("allow_editing")]
    [DefaultValue(true)]
    public required bool? AllowEditing { get; set; }

    [Column("allow_pets")]
    [DefaultValue(false)]
    public required bool AllowPets { get; set; }

    [Column("allow_pets_eat")]
    [DefaultValue(false)]
    public required bool AllowPetsEat { get; set; }

    [Column("trade_type")]
    [DefaultValue(RoomTradeType.Disabled)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required RoomTradeType TradeType { get; set; }

    [Column("mute_type")]
    [DefaultValue(RoomMuteType.None)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required RoomMuteType MuteType { get; set; }

    [Column("kick_type")]
    [DefaultValue(RoomKickType.None)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required RoomKickType KickType { get; set; }

    [Column("ban_type")]
    [DefaultValue(RoomBanType.None)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required RoomBanType BanType { get; set; }

    [Column("chat_mode_type")]
    [DefaultValue(RoomChatModeType.FreeFlow)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required RoomChatModeType ChatModeType { get; set; }

    [Column("chat_weight_type")]
    [DefaultValue(RoomChatWeightType.Normal)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required RoomChatWeightType ChatWeightType { get; set; }

    [Column("chat_speed_type")]
    [DefaultValue(RoomChatSpeedType.Normal)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required RoomChatSpeedType ChatSpeedType { get; set; }

    [Column("chat_protection_type")]
    [DefaultValue(RoomChatProtectionType.Minimal)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required RoomChatProtectionType ChatProtectionType { get; set; }

    [Column("chat_distance")]
    [DefaultValue(50)]
    public required int ChatDistance { get; set; }

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
}
