using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.FriendList.Enums;

namespace Turbo.Database.Entities.Messenger;

[Table("messenger_friends")]
[Index(nameof(PlayerEntityId), nameof(FriendPlayerEntityId), IsUnique = true)]
public class MessengerFriendEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("requested_id")]
    public required int FriendPlayerEntityId { get; set; }

    [Column("category_id")]
    public int? MessengerCategoryEntityId { get; set; }

    [Column("relation")]
    [DefaultValue(MessengerFriendRelationType.Zero)]
    public required MessengerFriendRelationType RelationType { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }

    [ForeignKey(nameof(FriendPlayerEntityId))]
    public required PlayerEntity FriendPlayerEntity { get; set; }

    [ForeignKey(nameof(MessengerCategoryEntityId))]
    public MessengerCategoryEntity? MessengerCategoryEntity { get; set; }
}
