using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Catalog;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// How much of one currency a player was paid for time online on <see cref="Day"/> (UTC), so an
/// online reward with a daily cap stops at it across logins and restarts. One row per player and
/// currency, started again on the first payout of a new day; only kept for capped rewards.
/// </summary>
[Table("player_online_rewards")]
[Index(nameof(PlayerEntityId), nameof(CurrencyTypeEntityId), IsUnique = true)]
public class PlayerOnlineRewardEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("currency_type_id")]
    public required int CurrencyTypeEntityId { get; set; }

    [Column("day")]
    public required DateOnly Day { get; set; }

    [Column("amount")]
    [DefaultValue(0)]
    public int Amount { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }

    [ForeignKey(nameof(CurrencyTypeEntityId))]
    public CurrencyTypeEntity? CurrencyTypeEntity { get; set; }
}
