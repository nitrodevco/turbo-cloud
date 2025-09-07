using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Turbo.Database.Attributes;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Room;

[TurboEntity]
[Table("room_chatlogs")]
public class RoomChatlogEntity : Entity
{
    [Column("room_id")]
    public required int RoomEntityId { get; set; }

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("target_player_id")]
    public int? TargetPlayerEntityId { get; set; }

    [Column("message")]
    [StringLength(100)]
    public required string Message { get; set; }

    [ForeignKey(nameof(RoomEntityId))]
    public required RoomEntity RoomEntity { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }

    [ForeignKey(nameof(TargetPlayerEntityId))]
    public PlayerEntity? TargetPlayerEntity { get; set; }
}
