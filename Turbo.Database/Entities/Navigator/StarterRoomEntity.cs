using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Room;

namespace Turbo.Database.Entities.Navigator;

/// <summary>
/// A room the hotel offers new players as a starter room (<c>:starterroom add</c>): each player who
/// picks it gets a copy. Its room id is the room type the client offers.
/// </summary>
[Table("starter_rooms")]
[Index(nameof(RoomEntityId), IsUnique = true)]
public class StarterRoomEntity : TurboEntity
{
    /// <summary>The room copied; the row goes when the room does.</summary>
    [Column("room_id")]
    public required int RoomEntityId { get; set; }

    /// <summary>Where it stands in the room picker, left to right.</summary>
    [Column("sort_order")]
    public int SortOrder { get; set; }

    /// <summary>The copy's name; <c>%name%</c> is the player's. Null keeps the room's own.</summary>
    [Column("name")]
    public string? Name { get; set; }

    /// <summary>The copy's description; <c>%name%</c> is the player's. Null keeps the room's own.</summary>
    [Column("description")]
    public string? Description { get; set; }

    /// <summary>When its picture was last taken; null when it has none yet.</summary>
    [Column("picture_taken_at")]
    public System.DateTime? PictureTakenAt { get; set; }

    [Column("created_by_player_id")]
    public int? CreatedByPlayerId { get; set; }

    [ForeignKey(nameof(RoomEntityId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public RoomEntity RoomEntity { get; set; } = null!;
}
