using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// One of Habbo's texts as it was last taken in. The next import compares against it to tell what
/// Habbo changed from what the hotel changed.
/// </summary>
[Table("habbo_texts")]
[Index(nameof(Key), IsUnique = true)]
public class HabboTextEntity : TurboEntity
{
    [Column("text_key")]
    [MaxLength(GamedataTextEntity.KEY_MAX_LENGTH)]
    public required string Key { get; set; }

    [Column("value", TypeName = "longtext")]
    public required string Value { get; set; }

    /// <summary>The version it was last taken in from.</summary>
    [Column("version_id")]
    public required int HabboTextVersionEntityId { get; set; }
}
