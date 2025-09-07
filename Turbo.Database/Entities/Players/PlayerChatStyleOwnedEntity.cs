using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Attributes;

namespace Turbo.Database.Entities.Players;

[TurboEntity]
[Table("player_chat_styles_owned")]
[Index(nameof(PlayerEntityId), nameof(ChatStyleId), IsUnique = true)]
public class PlayerChatStyleOwnedEntity : Entity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("chat_style_id")]
    public required int ChatStyleId { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }

    [ForeignKey(nameof(ChatStyleId))]
    public required PlayerChatStyleEntity ChatStyle { get; set; }
}
