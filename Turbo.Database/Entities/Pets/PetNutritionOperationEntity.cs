using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Pets;

[Comment("Pet feeding and drinking, one row per action, so a retry never feeds twice.")]
[Table("pet_nutrition_operations")]
public sealed class PetNutritionOperationEntity
{
    [Key, MaxLength(100)]
    public required string OperationId { get; set; }

    public int RoomId { get; set; }

    public int PetId { get; set; }

    public int OwnerId { get; set; }

    public int SupplierId { get; set; }

    public int BaseNutrition { get; set; }

    public int RequestedNutrition { get; set; }

    public int MaxNutrition { get; set; }

    public int NutritionAfter { get; set; }

    public int ActualGain { get; set; }

    public bool Completed { get; set; }

    /// <summary>When the row was written, so old receipts can be purged.</summary>
    [Column("created_at")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }
}
