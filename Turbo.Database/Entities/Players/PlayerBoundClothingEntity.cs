using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Furniture;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// Clothing furni a player has bound: a <c>figure_purchasable_set</c> furni they used, whose
/// figure sets are theirs since. The client is told the furni's class names
/// (<c>FigureSetIdsMessage.boundFurnitureNames</c>) and puts the clothes on, rather than asking
/// again, when the player uses another furni of the same kind.
/// </summary>
[Table("player_bound_clothing")]
[Index(nameof(PlayerEntityId), nameof(FurnitureDefinitionEntityId), IsUnique = true)]
public class PlayerBoundClothingEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("definition_id")]
    public required int FurnitureDefinitionEntityId { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }

    [ForeignKey(nameof(FurnitureDefinitionEntityId))]
    public FurnitureDefinitionEntity? FurnitureDefinitionEntity { get; set; }
}
