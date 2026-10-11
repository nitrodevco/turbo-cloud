using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Achievements;

[Comment(
    "Things players did that count toward achievements, waiting to be or already turned into progress."
)]
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

    [Column("occurred_at")]
    public DateTime OccurredAtUtc { get; set; }

    [Column(TypeName = "text")]
    [MaxLength(int.MaxValue)]
    public required string FactJson { get; set; }

    [Column(TypeName = "text")]
    [MaxLength(int.MaxValue)]
    public required string BindingsJson { get; set; }
    public bool Processed { get; set; }
}
