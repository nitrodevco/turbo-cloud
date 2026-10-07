using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// One of the hotel's texts, the external texts the client shows: Habbo's taken in, and the
/// hotel's own. The value is as the file writes it, <c>\n</c> escapes and all.
/// </summary>
[Table("gamedata_texts")]
[Index(nameof(Key), IsUnique = true)]
public class GamedataTextEntity : TurboEntity
{
    public const int KEY_MAX_LENGTH = 255;

    /// <summary>
    /// The collation a text key is kept under: binary, so keys that differ only in case are two
    /// keys, as they are to the client (Habbo has 62 such pairs: <c>ACH_PinataBreaker1_badge_name</c>
    /// and <c>ACH_pinatabreaker1_badge_name</c>).
    /// </summary>
    public const string KEY_COLLATION = "utf8mb4_bin";

    [Column("text_key")]
    [MaxLength(KEY_MAX_LENGTH)]
    public required string Key { get; set; }

    [Column("value", TypeName = "longtext")]
    public required string Value { get; set; }
}
