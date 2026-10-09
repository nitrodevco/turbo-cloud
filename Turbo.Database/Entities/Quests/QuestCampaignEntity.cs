using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Quests;

/// <summary>
/// A quest campaign: the quest window's rows, in <see cref="SortOrder"/>. The code names its
/// texts (quests.&lt;code&gt;.name) and images. A campaign with <see cref="EndsAt"/> is seasonal:
/// the window shows its time left.
/// </summary>
[Table("quest_campaigns")]
[Index(nameof(Code), IsUnique = true)]
public class QuestCampaignEntity : TurboEntity
{
    public const int CODE_MAX_LENGTH = 64;

    [Column("code")]
    [StringLength(CODE_MAX_LENGTH)]
    public required string Code { get; set; }

    [Column("sort_order")]
    [DefaultValue(0)]
    public int SortOrder { get; set; }

    [Column("enabled")]
    [DefaultValue(true)]
    public bool Enabled { get; set; } = true;

    /// <summary>When a seasonal campaign ends; null for one that does not.</summary>
    [Column("ends_at")]
    public DateTime? EndsAt { get; set; }
}
