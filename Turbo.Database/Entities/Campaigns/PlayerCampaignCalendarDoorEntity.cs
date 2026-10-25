using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Campaigns;

/// <summary>A calendar door a player opened: one row per player, campaign and day, so each is opened once.</summary>
[Table("player_campaign_calendar_doors")]
[Index(nameof(PlayerEntityId), nameof(CampaignCalendarEntityId), nameof(DayIndex), IsUnique = true)]
public class PlayerCampaignCalendarDoorEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("campaign_id")]
    public required int CampaignCalendarEntityId { get; set; }

    [Column("day_index")]
    public int DayIndex { get; set; }

    /// <summary>Opened with the staff force open rather than on its day.</summary>
    [Column("by_staff")]
    public bool ByStaff { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }

    [ForeignKey(nameof(CampaignCalendarEntityId))]
    public CampaignCalendarEntity? CampaignCalendarEntity { get; set; }
}
