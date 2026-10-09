using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Moderation;

/// <summary>A chat line sent with a call for help, in the order the reporter sent them.</summary>
[Table("cfh_report_chat_lines")]
[Index(nameof(ReportEntityId), nameof(Position))]
public class CfhReportChatLineEntity : TurboEntity
{
    public const int TEXT_MAX_LENGTH = 255;

    [Column("report_id")]
    public required int ReportEntityId { get; set; }

    [Column("position")]
    public required int Position { get; set; }

    /// <summary>Who said it; a plain column, so the line outlives the player.</summary>
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("text")]
    [StringLength(TEXT_MAX_LENGTH)]
    public required string Text { get; set; }

    [ForeignKey(nameof(ReportEntityId))]
    public CfhReportEntity? Report { get; set; }
}
