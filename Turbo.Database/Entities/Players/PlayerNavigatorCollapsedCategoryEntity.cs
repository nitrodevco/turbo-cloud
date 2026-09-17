using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

[Table("player_navigator_collapsed_categories")]
[Index(nameof(PlayerEntityId), nameof(SearchCode), IsUnique = true)]
public class PlayerNavigatorCollapsedCategoryEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("search_code")]
    [MaxLength(PlayerNavigatorSavedSearchEntity.SEARCH_CODE_MAX_LENGTH)]
    public required string SearchCode { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
