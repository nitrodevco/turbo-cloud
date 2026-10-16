using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// One bitmap of a chat style (<c>chat_bubble_base</c>, <c>chat_bubble_pointer</c>,
/// <c>selector_preview</c>, ...) as a PNG, packed into the habbo-free-flow-chat-com bundle under
/// <c>style_&lt;assetId&gt;_&lt;file&gt;</c>. It goes with its style.
/// </summary>
[Table("player_chat_style_bitmaps")]
[Index(nameof(ChatStyleId), nameof(File), IsUnique = true)]
public class PlayerChatStyleBitmapEntity : TurboEntity
{
    public const int FILE_MAX_LENGTH = 64;

    [Column("chat_style_id")]
    public required int ChatStyleId { get; set; }

    [Column("file")]
    [MaxLength(FILE_MAX_LENGTH)]
    public required string File { get; set; }

    [Column("data")]
    public required byte[] Data { get; set; }

    [ForeignKey(nameof(ChatStyleId))]
    public PlayerChatStyleEntity? ChatStyle { get; set; }
}
