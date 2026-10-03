using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Achievements;

[Table("achievement_audit")]
[Index(nameof(OperationId), IsUnique = true)]
public sealed class AchievementAuditEntity
{
    [Key]
    public long Id { get; set; }

    [MaxLength(160)]
    public required string OperationId { get; set; }
    public required string Actor { get; set; }
    public required string Reason { get; set; }

    [Column(TypeName = "longtext")]
    [MaxLength(int.MaxValue)]
    public string RequestJson { get; set; } = "{}";
    public DateTime OccurredAtUtc { get; set; }

    [Column(TypeName = "longtext")]
    [MaxLength(int.MaxValue)]
    public required string BeforeJson { get; set; }

    [Column(TypeName = "longtext")]
    [MaxLength(int.MaxValue)]
    public required string AfterJson { get; set; }
}
