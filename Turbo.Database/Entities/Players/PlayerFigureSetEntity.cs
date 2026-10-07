using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// A piece of clothing a player owns: a figure set the figure data marks as sold, which the
/// player may wear once they have it.
/// </summary>
[Table("player_figure_sets")]
[Index(nameof(PlayerEntityId), nameof(SetId), IsUnique = true)]
public class PlayerFigureSetEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("set_id")]
    public required int SetId { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
