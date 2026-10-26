using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Room;

namespace Turbo.Database.Entities.Navigator;

/// <summary>
/// A room in a Public tab category, where staff put it; <c>created_at</c> is when. A room can be in several categories
/// (Habbo listed Sunset Ranch in New and in Games), once in each.
/// </summary>
[Table("navigator_public_rooms")]
[Index(nameof(CategoryEntityId), nameof(RoomEntityId), IsUnique = true)]
public class NavigatorPublicRoomEntity : TurboEntity
{
    [Column("category_id")]
    public required int CategoryEntityId { get; set; }

    [Column("room_id")]
    public required int RoomEntityId { get; set; }

    [Column("order_num")]
    [DefaultValue(0)]
    public int OrderNum { get; set; }

    /// <summary>Who put it there: a player id, or null for a plugin or the server.</summary>
    [Column("added_by")]
    public int? AddedBy { get; set; }

    [ForeignKey(nameof(CategoryEntityId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public NavigatorPublicCategoryEntity CategoryEntity { get; set; } = null!;

    [ForeignKey(nameof(RoomEntityId))]
    [DeleteBehavior(DeleteBehavior.Cascade)]
    public RoomEntity RoomEntity { get; set; } = null!;
}
