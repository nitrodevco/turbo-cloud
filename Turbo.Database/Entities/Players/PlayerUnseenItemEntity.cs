using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Inventory;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// Something a player received and has not looked at yet: the client draws it as "new" until
/// they open its inventory tab. Kept across sessions, so what arrived while they were offline
/// (a gift, a trade) is still new when they log in. <see cref="ItemId"/> means what the category
/// says — a furni, pet, bot or badge row — so it has no foreign key of its own.
/// </summary>
[Table("player_unseen_items")]
[Index(nameof(PlayerEntityId), nameof(Category), nameof(ItemId), IsUnique = true)]
public class PlayerUnseenItemEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("category")]
    public required UnseenItemCategory Category { get; set; }

    [Column("item_id")]
    public required int ItemId { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
