using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Quests;

/// <summary>
/// A reward track prize, as the client draws it: <see cref="ProductType"/> (AS3
/// ProductIconWidget's product item type) and <see cref="RewardTypeId"/> (a point type, badge
/// code, sprite id, Habbicon id, pet or bot type), <see cref="ExtraParams"/> a pet's figure or a
/// bot's look. A furni prize can name the definition to give in
/// <see cref="FurnitureDefinitionId"/>, else the hotel's furni of that sprite is given.
/// </summary>
[Table("reward_track_prizes")]
[Index(nameof(RewardTrackEntityId), nameof(PrizeId), IsUnique = true)]
public class RewardTrackPrizeEntity : TurboEntity
{
    [Column("reward_track_id")]
    public int RewardTrackEntityId { get; set; }

    [Column("prize_id")]
    [StringLength(RewardTrackEntity.ID_MAX_LENGTH)]
    public required string PrizeId { get; set; }

    [Column("required_points")]
    public int RequiredPoints { get; set; }

    [Column("product_type")]
    public short ProductType { get; set; }

    [Column("reward_type_id")]
    [StringLength(255)]
    public string RewardTypeId { get; set; } = "";

    [Column("extra_params")]
    [StringLength(512)]
    public string ExtraParams { get; set; } = "";

    [Column("amount")]
    [DefaultValue(1)]
    public int Amount { get; set; } = 1;

    [Column("premium")]
    [DefaultValue(false)]
    public bool Premium { get; set; }

    [Column("furniture_definition_id")]
    public int? FurnitureDefinitionId { get; set; }

    /// <summary>The name a pet or bot prize is given; empty for the hotel's default.</summary>
    [Column("name")]
    [StringLength(64)]
    public string Name { get; set; } = "";

    [Column("sort_order")]
    [DefaultValue(0)]
    public int SortOrder { get; set; }

    [ForeignKey(nameof(RewardTrackEntityId))]
    public RewardTrackEntity? RewardTrackEntity { get; set; }
}
