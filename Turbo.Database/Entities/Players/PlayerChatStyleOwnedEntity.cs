using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

[Comment("The chat bubble styles each player owns.")]
[Table("player_chat_styles")]
[Index(nameof(PlayerEntityId), nameof(ChatStyleId), IsUnique = true)]
public class PlayerChatStyleOwnedEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    /// <summary>The style's client number (<c>hotel_chat_styles.id</c>).</summary>
    [Column("chat_style_id")]
    public required int ChatStyleId { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public required PlayerEntity PlayerEntity { get; set; }

    [ForeignKey(nameof(ChatStyleId))]
    public required ChatStyleEntity ChatStyle { get; set; }
}
