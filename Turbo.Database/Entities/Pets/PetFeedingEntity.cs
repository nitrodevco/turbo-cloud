using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Pets;

/// <summary>
/// A pet eating or drinking, written in the same transaction as the pet's new nutrition, so a
/// retried feed is answered from here rather than feeding twice.
/// </summary>
[Comment(
    "A pet eating or drinking, written with the pet's new nutrition so a retry never feeds twice."
)]
[Table("pet_feedings")]
[Index(nameof(PetId))]
public sealed class PetFeedingEntity
{
    /// <summary>The feed's id, given when it started; a retry carries the same one.</summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("room_id")]
    public int RoomId { get; set; }

    [Column("pet_id")]
    public int PetId { get; set; }

    [Column("owner_id")]
    public int OwnerId { get; set; }

    /// <summary>The player whose food or drink it was; 0 for none, such as a room's own bowl.</summary>
    [Column("fed_by_id")]
    public int FedById { get; set; }

    [Column("nutrition_before")]
    public int NutritionBefore { get; set; }

    /// <summary>What the food or drink offered; the pet takes what fits under the maximum.</summary>
    [Column("nutrition_offered")]
    public int NutritionOffered { get; set; }

    [Column("nutrition_max")]
    public int NutritionMax { get; set; }

    [Column("nutrition_after")]
    public int NutritionAfter { get; set; }

    [Column("created_at")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }
}
