using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Campaigns;

/// <summary>A campaign calendar: one door a day for <see cref="Days"/> days from <see cref="StartsAt"/> (UTC).</summary>
[Table("campaign_calendars")]
[Index(nameof(Name), IsUnique = true)]
public class CampaignCalendarEntity : TurboEntity
{
    public const int NAME_MAX_LENGTH = 64;
    public const int IMAGE_MAX_LENGTH = 255;

    [Column("name")]
    [StringLength(NAME_MAX_LENGTH)]
    public required string Name { get; set; }

    [Column("image")]
    [StringLength(IMAGE_MAX_LENGTH)]
    public required string Image { get; set; }

    [Column("starts_at")]
    public DateTime StartsAt { get; set; }

    [Column("days")]
    public int Days { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; } = true;

    public List<CampaignCalendarRewardEntity> Rewards { get; set; } = [];
}
