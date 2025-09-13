using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Messenger;

[Table("messenger_requests")]
[Index(nameof(PlayerEntityId), nameof(RequestedPlayerEntityId), IsUnique = true)]
public class MessengerRequestEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("requested_id")]
    public required int RequestedPlayerEntityId { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }

    [ForeignKey(nameof(RequestedPlayerEntityId))]
    public required PlayerEntity RequestedPlayerEntity { get; set; }
}
