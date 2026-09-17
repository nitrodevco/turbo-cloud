using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Navigator.Enums;

namespace Turbo.Database.Entities.Players;

[Table("player_navigator_view_modes")]
[Index(nameof(PlayerEntityId), nameof(SearchCode), IsUnique = true)]
public class PlayerNavigatorViewModeEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("search_code")]
    [MaxLength(PlayerNavigatorSavedSearchEntity.SEARCH_CODE_MAX_LENGTH)]
    public required string SearchCode { get; set; }

    [Column("view_mode")]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required NavigatorViewModeType ViewMode { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
