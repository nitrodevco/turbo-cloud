using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turbo.Database.Entities.Achievements;

[Table("pet_respect_operations")]
public sealed class PetRespectOperationEntity
{
    [Key, MaxLength(100)]
    public required string OperationId { get; set; }

    public int RoomId { get; set; }

    public int ActorId { get; set; }

    public int PetId { get; set; }

    public int OwnerId { get; set; }

    public int BaseRespect { get; set; }

    public bool Rejected { get; set; }

    public bool Completed { get; set; }

    public int ResultRespect { get; set; }
}
