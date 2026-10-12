using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// One of the client's external variables, the configuration it loads from the gamedata host:
/// its key, and its value as JSON (<c>"text"</c>, <c>true</c>, <c>120</c>, <c>[1, 2]</c>). A
/// variable linked to a server setting (<see cref="SettingPath"/>) or to one of the hotel's
/// gamedata files (<see cref="LinkedFile"/>) is written with the setting's value, or the file's
/// address by hash, instead, and follows it. It follows one or the other, never both.
/// </summary>
[Comment(
    "One of the client's external variables, the configuration it loads from the gamedata host: its key, and its value as JSON (\"text\", true, 120, [1, 2])."
)]
[Table("gamedata_variables")]
[Index(nameof(Key), IsUnique = true)]
public class GamedataVariableEntity : TurboEntity
{
    public const int KEY_MAX_LENGTH = 255;

    /// <summary>
    /// The collation a key is kept under: PostgreSQL's byte-order <c>C</c>, so keys that differ
    /// only in case are two keys, as they are to the client.
    /// </summary>
    public const string KEY_COLLATION = "C";

    [Column("variable_key")]
    [MaxLength(KEY_MAX_LENGTH)]
    public required string Key { get; set; }

    [Column("value", TypeName = "text")]
    public required string Value { get; set; }

    /// <summary>
    /// The server setting the variable takes its value from (<c>Turbo:Web:HotelName</c>); null for
    /// one with a value of its own. <see cref="Value"/> is then the setting's value when it was
    /// linked, written should the setting ever go.
    /// </summary>
    [Column("setting_path")]
    [MaxLength(Settings.ServerSettingEntity.PATH_MAX_LENGTH)]
    public string? SettingPath { get; set; }

    public const int FILE_MAX_LENGTH = 64;

    /// <summary>
    /// The gamedata file whose address the variable takes, by its current hash
    /// (<c>furnidata_json</c>, for <c>furnituredata.url</c>); null for one that doesn't.
    /// <see cref="Value"/> is written while the hotel has no public address to build it on.
    /// </summary>
    [Column("linked_file")]
    [MaxLength(FILE_MAX_LENGTH)]
    public string? LinkedFile { get; set; }
}
