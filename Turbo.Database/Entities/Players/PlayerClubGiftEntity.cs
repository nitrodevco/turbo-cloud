using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// One Habbo Club gift a player has claimed. There is a row per claim, not per gift: a member
/// earns a gift for every period of membership they use up and may pick the same one again, so
/// what matters is how many they have taken against how many they have earned. Which offer they
/// picked is kept because it is the only record of what was handed out.
/// </summary>
[Table("player_club_gifts")]
[Index(nameof(PlayerEntityId))]
public class PlayerClubGiftEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    /// <summary>
    /// The offer that was handed out. Deliberately not a foreign key: this is a record of what
    /// happened, and taking a gift off the shelf must not erase the claims it was spent on —
    /// that would give every member who picked it their gift back.
    /// </summary>
    [Column("offer_id")]
    public required int CatalogOfferEntityId { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
