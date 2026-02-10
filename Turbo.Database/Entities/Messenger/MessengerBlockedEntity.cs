using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Messenger;

[Table("messenger_blocked")]
[Index(nameof(PlayerEntityId), nameof(BlockedPlayerEntityId), IsUnique = true)]
public class MessengerBlockedEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("blocked_player_id")]
    public required int BlockedPlayerEntityId { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }

    [ForeignKey(nameof(BlockedPlayerEntityId))]
    public required PlayerEntity BlockedPlayerEntity { get; set; }
}
