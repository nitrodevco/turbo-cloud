using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// One of Habbo's external variables as it was last taken in, as Habbo's file writes it (bare
/// text). The next update compares against it to tell what Habbo changed from what the hotel
/// changed, and which of the hotel's variables came from Habbo when Habbo drops one.
/// </summary>
[Table("habbo_variables")]
[Index(nameof(Key), IsUnique = true)]
public class HabboVariableEntity : TurboEntity
{
    [Column("variable_key")]
    [MaxLength(GamedataVariableEntity.KEY_MAX_LENGTH)]
    public required string Key { get; set; }

    [Column("value", TypeName = "longtext")]
    public required string Value { get; set; }

    /// <summary>The version it was last taken in from.</summary>
    [Column("version_id")]
    public required int HabboVariableVersionEntityId { get; set; }
}
