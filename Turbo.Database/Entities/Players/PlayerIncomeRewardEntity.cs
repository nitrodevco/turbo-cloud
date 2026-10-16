using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// One earning waiting in a player's earnings vault (Flash's <c>IncomeReward</c>): a category of
/// <c>EarningsView</c>, what it pays and how much. <see cref="Reference"/> names what paid it in, so
/// a payer that retries cannot deposit twice; a claimed row stays, with <see cref="ClaimedAt"/>,
/// for the same reason.
/// </summary>
[Table("player_income_rewards")]
[Index(nameof(PlayerEntityId), nameof(Reference), IsUnique = true)]
public class PlayerIncomeRewardEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    /// <summary>The <c>EarningsView</c> category index (VaultRewardCategoryType).</summary>
    [Column("category")]
    public required int Category { get; set; }

    /// <summary>0 duckets, 1 credits (VaultRewardType).</summary>
    [Column("reward_type")]
    public required int RewardType { get; set; }

    [Column("amount")]
    public required int Amount { get; set; }

    [Column("product_code")]
    [MaxLength(64)]
    public string ProductCode { get; set; } = "";

    [Column("reference")]
    [MaxLength(128)]
    public required string Reference { get; set; }

    /// <summary>The claim paying this row out; set before the purse is credited, so a claim that
    /// stopped half way is finished by the next one under the same wallet reference.</summary>
    [Column("claim_reference")]
    [MaxLength(64)]
    public string? ClaimReference { get; set; }

    [Column("claimed_at")]
    public DateTime? ClaimedAt { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
