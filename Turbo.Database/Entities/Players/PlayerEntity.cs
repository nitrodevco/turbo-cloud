using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Contracts.Enums.Players;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Messenger;
using Turbo.Database.Entities.Room;
using Turbo.Database.Entities.Security;

namespace Turbo.Database.Entities.Players;

[Table("players")]
[Index(nameof(Name), IsUnique = true)]
public class PlayerEntity : TurboEntity
{
    [Column("name")]
    public required string Name { get; set; }

    [Column("motto")]
    public string? Motto { get; set; }

    [Column("figure")]
    [MaxLength(100)]
    [DefaultValue("hr-115-42.hd-195-19.ch-3030-82.lg-275-1408.fa-1201.ca-1804-64")]
    public required string Figure { get; set; }

    [Column("gender")]
    [DefaultValue(AvatarGender.Male)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required AvatarGender Gender { get; set; }

    [Column("status")]
    [DefaultValue(PlayerStatusEnum.Offline)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required PlayerStatusEnum PlayerStatus { get; set; }

    [Column("perk_flags")]
    [DefaultValue(PlayerPerkEnum.None)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required PlayerPerkEnum PlayerPerks { get; set; }

    [Column("room_chat_style_id")]
    public int? RoomChatStyleId { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<PlayerBadgeEntity>? PlayerBadges { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<PlayerCurrencyEntity>? PlayerCurrencies { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<PlayerChatStyleOwnedEntity>? PlayerOwnedChatStyles { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<FurnitureEntity>? Furniture { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<MessengerCategoryEntity>? MessengerCategories { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<MessengerFriendEntity>? MessengerFriends { get; set; }

    [InverseProperty("RequestedPlayerEntity")]
    public List<MessengerRequestEntity>? MessengerRequests { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<MessengerRequestEntity>? MessengerRequestsSent { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<SecurityTicketEntity>? SecurityTickets { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<RoomEntity>? Rooms { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<RoomBanEntity>? RoomBans { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<RoomMuteEntity>? RoomMutes { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<RoomRightEntity>? RoomRights { get; set; }

    [InverseProperty("PlayerEntity")]
    public List<RoomChatlogEntity>? RoomChatlogs { get; set; }
}
