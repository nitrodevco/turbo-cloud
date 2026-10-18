using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Quests;

/// <summary>
/// A reward track (Progression menu): <see cref="TrackId"/> names its <c>reward_track.&lt;id&gt;.*</c>
/// texts, players get the enabled ones within their dates in <see cref="SortOrder"/>. With
/// <see cref="HasPremium"/> it sells a premium for the costs here, which multiplies task points by
/// <see cref="PremiumTaskPointsBoost"/> and gives <see cref="PremiumInstantPoints"/>.
/// </summary>
[Table("reward_tracks")]
[Index(nameof(TrackId), IsUnique = true)]
public class RewardTrackEntity : TurboEntity
{
    public const int ID_MAX_LENGTH = 64;

    [Column("track_id")]
    [StringLength(ID_MAX_LENGTH)]
    public required string TrackId { get; set; }

    [Column("theme")]
    [StringLength(32)]
    public string Theme { get; set; } = "";

    [Column("enabled")]
    [DefaultValue(true)]
    public bool Enabled { get; set; } = true;

    [Column("starts_at")]
    public DateTime? StartsAt { get; set; }

    [Column("ends_at")]
    public DateTime? EndsAt { get; set; }

    [Column("sort_order")]
    [DefaultValue(0)]
    public int SortOrder { get; set; }

    [Column("has_premium")]
    [DefaultValue(false)]
    public bool HasPremium { get; set; }

    [Column("premium_task_points_boost")]
    [DefaultValue(1d)]
    public double PremiumTaskPointsBoost { get; set; } = 1;

    [Column("premium_instant_points")]
    [DefaultValue(0)]
    public int PremiumInstantPoints { get; set; }

    [Column("premium_cost_credits")]
    [DefaultValue(0)]
    public int PremiumCostCredits { get; set; }

    [Column("premium_cost_diamonds")]
    [DefaultValue(0)]
    public int PremiumCostDiamonds { get; set; }

    public List<RewardTrackTaskEntity> Tasks { get; set; } = [];

    public List<RewardTrackPrizeEntity> Prizes { get; set; } = [];
}
