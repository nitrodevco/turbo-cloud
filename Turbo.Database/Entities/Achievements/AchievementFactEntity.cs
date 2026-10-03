using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Achievements;

[Table("achievement_facts")]
[Index(nameof(PlayerId), nameof(Processed), nameof(Id))]
[Index(nameof(PlayerId), nameof(OperationId), IsUnique = true)]
public sealed class AchievementFactEntity
{
    [Key]
    public long Id { get; set; }
    public int PlayerId { get; set; }

    [MaxLength(64)]
    public required string Source { get; set; }

    [MaxLength(160)]
    public required string OperationId { get; set; }
    public DateTime OccurredAtUtc { get; set; }

    [Column(TypeName = "longtext")]
    [MaxLength(int.MaxValue)]
    public required string FactJson { get; set; }

    [Column(TypeName = "longtext")]
    [MaxLength(int.MaxValue)]
    public required string BindingsJson { get; set; }
    public bool Processed { get; set; }
}
