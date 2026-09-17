using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Navigator;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Room;

[Table("room_events")]
[Index(nameof(RoomEntityId))]
[Index(nameof(ExpiresAt))]
public class RoomEventEntity : TurboEntity
{
    public const int NAME_MAX_LENGTH = 64;
    public const int DESCRIPTION_MAX_LENGTH = 256;

    [Column("room_id")]
    public required int RoomEntityId { get; set; }

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("category_id")]
    public required int NavigatorEventCategoryEntityId { get; set; }

    [Column("name")]
    [MaxLength(NAME_MAX_LENGTH)]
    public required string Name { get; set; }

    [Column("description")]
    [MaxLength(DESCRIPTION_MAX_LENGTH)]
    public required string Description { get; set; }

    [Column("expires_at")]
    public required DateTime ExpiresAt { get; set; }

    [ForeignKey(nameof(RoomEntityId))]
    public RoomEntity? RoomEntity { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }

    [ForeignKey(nameof(NavigatorEventCategoryEntityId))]
    public NavigatorEventCategoryEntity? NavigatorEventCategoryEntity { get; set; }
}
