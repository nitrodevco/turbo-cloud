using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turbo.Database.Entities.Pets;

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
}
