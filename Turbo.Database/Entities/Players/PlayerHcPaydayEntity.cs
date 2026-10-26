using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// A player's catalogue credit spend towards one HC Payday (<see cref="Payday"/>, UTC), as the club
/// centre shows it (<c>ScrSendKickbackInfo</c>): what they spent while in Habbo Club and what they
/// spent while not. One row per player, started again by the first purchase towards a later
/// payday; a row for a payday gone by counts as nothing.
/// </summary>
[Table("player_hc_paydays")]
[Index(nameof(PlayerEntityId), IsUnique = true)]
public class PlayerHcPaydayEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("payday")]
    public required DateOnly Payday { get; set; }

    [Column("credits_spent")]
    [DefaultValue(0)]
    public int CreditsSpent { get; set; }

    [Column("credits_missed")]
    [DefaultValue(0)]
    public int CreditsMissed { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
