using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Database.Entities.Pets;

/// <summary>
/// One scratch a player gave a pet. Like a respect between players it changes the giver (a pet
/// respect spent) and the pet, so it is written as pending first and finished, once, by its id.
/// </summary>
[Comment("One scratch a player gave a pet: who, which pet, and whether it went through.")]
[Table("pet_respects")]
[Index(nameof(PetId))]
public sealed class PetRespectEntity
{
    /// <summary>The scratch's id, given when the player clicked; a retry carries the same one.</summary>
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    /// <summary>The room it happened in, whose grain finishes it.</summary>
    [Column("room_id")]
    public int RoomId { get; set; }

    [Column("giver_id")]
    public int GiverId { get; set; }

    [Column("pet_id")]
    public int PetId { get; set; }

    [Column("owner_id")]
    public int OwnerId { get; set; }

    /// <summary>The pet's respect when the scratch started.</summary>
    [Column("respect_before")]
    public int RespectBefore { get; set; }

    [Column("status")]
    [DefaultValue(RespectStatus.Pending)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public RespectStatus Status { get; set; }

    /// <summary>The pet's respect once given, which a retry is answered with.</summary>
    [Column("respect_after")]
    public int RespectAfter { get; set; }

    [Column("created_at")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }
}
