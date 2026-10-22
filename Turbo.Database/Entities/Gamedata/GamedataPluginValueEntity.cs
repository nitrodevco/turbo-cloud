using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// An external variable or text a plugin brought, and the value it last gave: the hotel's row of
/// that key is the plugin's own while it still holds this value, and the hotel's once it was
/// changed. A plugin's own are sent to clients only while it is loaded, and follow the plugin's
/// value when a new version of it gives another.
/// </summary>
[Table("gamedata_plugin_values")]
[Index(nameof(RecordType), nameof(Key), IsUnique = true)]
[Index(nameof(PluginKey))]
public class GamedataPluginValueEntity : TurboEntity
{
    public const int PLUGIN_KEY_MAX_LENGTH = 64;

    [Column("plugin_key")]
    [MaxLength(PLUGIN_KEY_MAX_LENGTH)]
    public required string PluginKey { get; set; }

    /// <summary><see cref="GamedataRecordType.Variable"/> or <see cref="GamedataRecordType.Text"/>.</summary>
    [Column("record_type")]
    public required GamedataRecordType RecordType { get; set; }

    [Column("record_key")]
    [MaxLength(GamedataVariableEntity.KEY_MAX_LENGTH)]
    public required string Key { get; set; }

    /// <summary>The value as the hotel keeps it: a variable's normalised JSON, a text as the file writes it.</summary>
    [Column("value", TypeName = "longtext")]
    public required string Value { get; set; }
}
