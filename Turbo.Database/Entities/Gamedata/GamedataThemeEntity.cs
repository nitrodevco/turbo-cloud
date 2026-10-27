using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Plugins;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// Who a theme (a data plugin with a <c>theme.json</c>, or the hotel's own) reaches: its status,
/// whether it is the hotel's default and whether players may choose it, and while it is in
/// development the players and permission groups that get it. A theme with no row is Live, not the
/// default and not to be chosen.
/// </summary>
[Table("gamedata_themes")]
[Index(nameof(PluginKey), IsUnique = true)]
public class GamedataThemeEntity : TurboEntity
{
    [Column("plugin_key")]
    [MaxLength(GamedataPluginValueEntity.PLUGIN_KEY_MAX_LENGTH)]
    public required string PluginKey { get; set; }

    [Column("status")]
    [DefaultValue(ThemeStatus.Live)]
    public ThemeStatus Status { get; set; }

    [Column("is_default")]
    [DefaultValue(false)]
    public bool IsDefault { get; set; }

    [Column("selectable")]
    [DefaultValue(false)]
    public bool Selectable { get; set; }

    /// <summary>The players a development theme reaches, by id: a JSON array (<c>[101, 102]</c>).</summary>
    [Column("audience_players", TypeName = "longtext")]
    public string AudiencePlayers { get; set; } = "[]";

    /// <summary>The permission groups a development theme reaches, by name: a JSON array.</summary>
    [Column("audience_groups", TypeName = "longtext")]
    public string AudienceGroups { get; set; } = "[]";

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; }
}
