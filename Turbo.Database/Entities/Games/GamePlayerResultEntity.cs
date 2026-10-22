using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Games;

/// <summary>
/// A player's part in one game-centre game: written when the game starts (it counts as played,
/// and against the day's free games), its score set when the game reports how it ended. The
/// leaderboards sum the scores, weekly by <see cref="Year"/> and <see cref="Week"/>.
/// </summary>
[Table("game_player_results")]
[Index(nameof(GameTypeId), nameof(GameId), nameof(PlayerEntityId), IsUnique = true)]
[Index(nameof(GameTypeId), nameof(Year), nameof(Week))]
[Index(nameof(PlayerEntityId), nameof(GameTypeId))]
public class GamePlayerResultEntity : TurboEntity
{
    [Column("game_type_id")]
    public required int GameTypeId { get; set; }

    [Column("game_id")]
    public required int GameId { get; set; }

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("team_id")]
    public int TeamId { get; set; }

    [Column("score")]
    public int Score { get; set; }

    /// <summary>Where the player finished, 1 first; 0 until the game reports.</summary>
    [Column("placement")]
    public int Placement { get; set; }

    /// <summary>The ISO year and week the game started in.</summary>
    [Column("year")]
    public required int Year { get; set; }

    [Column("week")]
    public required int Week { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
