using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Database.Entities.Furniture;

[Table("furniture_definitions")]
[Index(nameof(SpriteId), nameof(ProductType), nameof(FurniCategory), IsUnique = true)]
public class FurnitureDefinitionEntity : TurboEntity
{
    [Column("sprite_id")]
    public required int SpriteId { get; set; }

    [Column("name")]
    public required string Name { get; set; }

    [Column("type")]
    [DefaultValue(ProductType.Floor)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required ProductType ProductType { get; set; }

    [Column("category")]
    [DefaultValue(FurnitureCategory.Default)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required FurnitureCategory FurniCategory { get; set; }

    [Column("logic")]
    [MaxLength(50)]
    [DefaultValue("none")] // RoomObjectLogicType.FurnitureDefault
    public required string Logic { get; set; }

    [Column("total_states")]
    [DefaultValue(0)]
    public int TotalStates { get; set; }

    [Column("width")]
    [DefaultValue(0)]
    public required int Width { get; set; } // columns

    [Column("length")]
    [DefaultValue(1)]
    public required int Length { get; set; } // rows

    [Column("stack_height", TypeName = "double(10,3)")]
    [DefaultValue(0.0d)]
    public required double StackHeight { get; set; } // depth

    [Column("can_stack")]
    [DefaultValue(true)]
    public required bool CanStack { get; set; }

    [Column("can_walk")]
    [DefaultValue(false)]
    public required bool CanWalk { get; set; }

    [Column("can_sit")]
    [DefaultValue(false)]
    public required bool CanSit { get; set; }

    [Column("can_lay")]
    [DefaultValue(false)]
    public required bool CanLay { get; set; }

    [Column("can_recycle")]
    [DefaultValue(false)]
    public required bool CanRecycle { get; set; }

    [Column("can_trade")]
    [DefaultValue(true)]
    public required bool CanTrade { get; set; }

    [Column("can_group")]
    [DefaultValue(true)]
    public required bool CanGroup { get; set; }

    [Column("can_sell")]
    [DefaultValue(true)]
    public required bool CanSell { get; set; }

    [Column("usage_policy")]
    [DefaultValue(FurnitureUsageType.Controller)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public FurnitureUsageType UsagePolicy { get; set; }

    [Column("extra_data")]
    public string? ExtraData { get; set; }

    public List<FurnitureEntity>? Furnitures { get; set; }
}
