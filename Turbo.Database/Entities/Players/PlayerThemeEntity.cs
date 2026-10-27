using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Gamedata;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// The theme a player chose to draw the hotel with (<see cref="GamedataThemeEntity"/>), by its
/// plugin key; no row is the hotel's default. Kept while the theme may be chosen, ignored after.
/// </summary>
[Table("player_themes")]
[Index(nameof(PlayerEntityId), IsUnique = true)]
public class PlayerThemeEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("plugin_key")]
    [MaxLength(GamedataPluginValueEntity.PLUGIN_KEY_MAX_LENGTH)]
    public required string PluginKey { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
