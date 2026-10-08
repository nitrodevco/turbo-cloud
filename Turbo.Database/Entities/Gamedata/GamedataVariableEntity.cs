using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// One of the client's external variables, the configuration it loads from the gamedata host:
/// its key, and its value as JSON (<c>"text"</c>, <c>true</c>, <c>120</c>, <c>[1, 2]</c>).
/// </summary>
[Table("gamedata_variables")]
[Index(nameof(Key), IsUnique = true)]
public class GamedataVariableEntity : TurboEntity
{
    public const int KEY_MAX_LENGTH = 255;

    /// <summary>
    /// The collation a key is kept under: binary, so keys that differ only in case are two keys,
    /// as they are to the client.
    /// </summary>
    public const string KEY_COLLATION = "utf8mb4_bin";

    [Column("variable_key")]
    [MaxLength(KEY_MAX_LENGTH)]
    public required string Key { get; set; }

    [Column("value", TypeName = "longtext")]
    public required string Value { get; set; }
}
