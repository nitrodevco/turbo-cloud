using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Pets;

[Comment("Respect given to pets (scratches), one row per action, so a retry never counts twice.")]
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

    /// <summary>When the row was written, so old receipts can be purged.</summary>
    [Column("created_at")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }
}
