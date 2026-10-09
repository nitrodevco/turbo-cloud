using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Moderation.Enums;

namespace Turbo.Database.Entities.Moderation;

/// <summary>
/// A call for help. The ids are plain columns, not foreign keys, so a report outlives the
/// players and the room it names, as a sanction does. <c>created_at</c> is when it was sent.
/// </summary>
[Table("cfh_reports")]
[Index(nameof(ReporterEntityId), nameof(ClosedAt))]
public class CfhReportEntity : TurboEntity
{
    public const int MESSAGE_MAX_LENGTH = 1000;
    public const int NAME_MAX_LENGTH = 100;
    public const int EMAIL_MAX_LENGTH = 255;

    public const int EXTRA_DATA_ID_MAX_LENGTH = 64;

    [Column("reporter_id")]
    public required int ReporterEntityId { get; set; }

    /// <summary>Where it was sent from, which says what <see cref="ChatLines"/> are and whether the photo columns hold anything.</summary>
    [Column("source")]
    [DefaultValue(CfhSourceType.Room)]
    public CfhSourceType Source { get; set; }

    /// <summary>A photo report's photo: its extra data id; null otherwise.</summary>
    [Column("extra_data_id")]
    [StringLength(EXTRA_DATA_ID_MAX_LENGTH)]
    public string? ExtraDataId { get; set; }

    /// <summary>A photo report's wall item; null otherwise. A plain column: the photo may be gone.</summary>
    [Column("item_id")]
    public int? ItemEntityId { get; set; }

    /// <summary>The player reported; null for a report about a room or no one.</summary>
    [Column("reported_id")]
    public int? ReportedEntityId { get; set; }

    [Column("room_id")]
    public int? RoomEntityId { get; set; }

    [Column("topic_id")]
    public required int TopicId { get; set; }

    [Column("message")]
    [StringLength(MESSAGE_MAX_LENGTH)]
    public required string Message { get; set; }

    /// <summary>The name an unlawful activity report gives; null otherwise.</summary>
    [Column("reporter_name")]
    [StringLength(NAME_MAX_LENGTH)]
    public string? ReporterName { get; set; }

    /// <summary>The email an unlawful activity report gives; null otherwise.</summary>
    [Column("reporter_email")]
    [StringLength(EMAIL_MAX_LENGTH)]
    public string? ReporterEmail { get; set; }

    /// <summary>When staff closed it; null while it is open.</summary>
    [Column("closed_at")]
    public DateTime? ClosedAt { get; set; }

    [Column("sanctioned")]
    [DefaultValue(false)]
    public bool Sanctioned { get; set; }

    [Column("auto_moderated")]
    [DefaultValue(false)]
    public bool AutoModerated { get; set; }

    [Column("appeal_status")]
    [DefaultValue(CfhAppealStatusType.None)]
    public CfhAppealStatusType AppealStatus { get; set; }

    [Column("appeal_created_at")]
    public DateTime? AppealCreatedAt { get; set; }

    [Column("appeal_resolved_at")]
    public DateTime? AppealResolvedAt { get; set; }

    public IList<CfhReportChatLineEntity>? ChatLines { get; set; }
}
