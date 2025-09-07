using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Attributes;

namespace Turbo.Database.Entities.Furniture;

[TurboEntity]
[Table("furniture_teleport_links")]
[Index(nameof(FurnitureEntityOneId), IsUnique = true)]
[Index(nameof(FurnitureEntityTwoId), IsUnique = true)]
public class FurnitureTeleportLinkEntity : Entity
{
    [Column("furniture_one_id")]
    public int FurnitureEntityOneId { get; set; }

    [Column("furniture_two_id")]
    public int FurnitureEntityTwoId { get; set; }

    [ForeignKey(nameof(FurnitureEntityOneId))]
    public required FurnitureEntity FurnitureEntityOne { get; set; }

    [ForeignKey(nameof(FurnitureEntityTwoId))]
    public required FurnitureEntity FurnitureEntityTwo { get; set; }
}
