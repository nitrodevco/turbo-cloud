using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Attributes;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Messenger;

[TurboEntity]
[Table("messenger_categories")]
[Index(nameof(PlayerEntityId), nameof(Name), IsUnique = true)]
public class MessengerCategoryEntity : Entity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("name")]
    public required string Name { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }
}
