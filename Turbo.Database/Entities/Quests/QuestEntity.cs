using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Quests;

/// <summary>
/// One quest of a campaign, done in <see cref="SortOrder"/>. <see cref="LocalizationCode"/> names
/// its texts (quests.&lt;campaign&gt;.&lt;code&gt;.name, .desc, .hint, .completed) and image;
/// <see cref="Type"/> is what counts towards it and <see cref="Target"/>, when set, the one value
/// that counts (a badge code for WEAR_BADGE).
/// </summary>
[Table("quests")]
[Index(nameof(CampaignEntityId), nameof(LocalizationCode), IsUnique = true)]
public class QuestEntity : TurboEntity
{
    public const int CODE_MAX_LENGTH = 64;
    public const int TARGET_MAX_LENGTH = 128;
    public const int IMAGE_VERSION_MAX_LENGTH = 16;

    [Column("campaign_id")]
    public required int CampaignEntityId { get; set; }

    [Column("localization_code")]
    [StringLength(CODE_MAX_LENGTH)]
    public required string LocalizationCode { get; set; }

    [Column("type")]
    [StringLength(CODE_MAX_LENGTH)]
    public required string Type { get; set; }

    [Column("target")]
    [StringLength(TARGET_MAX_LENGTH)]
    public string Target { get; set; } = "";

    [Column("total_steps")]
    [DefaultValue(1)]
    public int TotalSteps { get; set; } = 1;

    [Column("activity_point_type")]
    [DefaultValue(0)]
    public int ActivityPointType { get; set; }

    [Column("reward_amount")]
    [DefaultValue(0)]
    public int RewardAmount { get; set; }

    [Column("sort_order")]
    [DefaultValue(0)]
    public int SortOrder { get; set; }

    [Column("image_version")]
    [StringLength(IMAGE_VERSION_MAX_LENGTH)]
    public string ImageVersion { get; set; } = "";

    [Column("catalog_page_name")]
    [StringLength(CODE_MAX_LENGTH)]
    public string CatalogPageName { get; set; } = "";

    /// <summary>A seasonal quest's chain (quests.&lt;campaign&gt;.&lt;chain&gt;.chaincaption).</summary>
    [Column("chain_code")]
    [StringLength(CODE_MAX_LENGTH)]
    public string ChainCode { get; set; } = "";

    [Column("easy")]
    [DefaultValue(false)]
    public bool Easy { get; set; }

    [ForeignKey(nameof(CampaignEntityId))]
    public QuestCampaignEntity? Campaign { get; set; }
}
