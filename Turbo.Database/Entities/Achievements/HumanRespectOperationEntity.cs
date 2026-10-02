using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turbo.Database.Entities.Achievements;

[Table("human_respect_operations")]
public sealed class HumanRespectOperationEntity
{
    [Key, MaxLength(100)]
    public required string OperationId { get; set; }

    public int ActorId { get; set; }

    public int TargetId { get; set; }

    public bool Rejected { get; set; }

    public bool Completed { get; set; }

    public int ResultTotal { get; set; }
}
