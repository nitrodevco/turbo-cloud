using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Room;

namespace Turbo.Database.Entities.Players;

[Table("player_favourite_rooms")]
[Index(nameof(PlayerEntityId), nameof(RoomEntityId), IsUnique = true)]
public class PlayerFavoriteRoomsEntity : Entity
{
    [Column("player_id")]
    [Required]
    public int PlayerEntityId { get; set; }

    [Column("room_id")]
    [Required]
    public int RoomEntityId { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity PlayerEntity { get; set; }

    [ForeignKey(nameof(RoomEntityId))]
    public RoomEntity RoomEntity { get; set; }
}
