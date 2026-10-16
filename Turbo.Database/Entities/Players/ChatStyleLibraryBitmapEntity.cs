using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// A bitmap of the habbo-free-flow-chat-com library that is no style's (the client's chat UI
/// draws it), kept as an imported bundle had it so a bundle built here still carries it.
/// </summary>
[Table("chat_style_library_bitmaps")]
[Index(nameof(Name), IsUnique = true)]
public class ChatStyleLibraryBitmapEntity : TurboEntity
{
    public const int NAME_MAX_LENGTH = 128;

    /// <summary>Its name in the bundle, the library's name and a hyphen before it.</summary>
    [Column("name")]
    [MaxLength(NAME_MAX_LENGTH)]
    public required string Name { get; set; }

    [Column("data")]
    public required byte[] Data { get; set; }
}
