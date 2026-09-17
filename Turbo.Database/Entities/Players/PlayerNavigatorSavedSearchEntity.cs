using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

[Table("player_navigator_saved_searches")]
[Index(nameof(PlayerEntityId), nameof(SearchId), IsUnique = true)]
public class PlayerNavigatorSavedSearchEntity : TurboEntity
{
    public const int SEARCH_CODE_MAX_LENGTH = 64;
    public const int FILTER_MAX_LENGTH = 128;

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    /// <summary>The id the client uses for this search, unique per player.</summary>
    [Column("search_id")]
    public required int SearchId { get; set; }

    [Column("search_code")]
    [MaxLength(SEARCH_CODE_MAX_LENGTH)]
    public required string SearchCode { get; set; }

    [Column("filter")]
    [MaxLength(FILTER_MAX_LENGTH)]
    public required string Filter { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
