using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Database.Attributes;

namespace Turbo.Database.Entities.Furniture;

[TurboEntity]
[Table("furniture_definitions")]
[Index(nameof(SpriteId), nameof(Type), IsUnique = true)]
public class FurnitureDefinitionEntity : Entity
{
    [Column("sprite_id")]
    public required int SpriteId { get; set; }

    [Column("public_name")]
    public required string PublicName { get; set; }

    [Column("product_name")]
    public required string ProductName { get; set; }

    [Column("type")]
    [DefaultValue(FurniTypeEnum.Floor)]
    public required FurniTypeEnum Type { get; set; }

    [Column("logic")]
    [MaxLength(30)]
    [DefaultValue("none")] // RoomObjectLogicType.FurnitureDefault
    public required string Logic { get; set; }

    [Column("total_states")]
    [DefaultValue(0)]
    public int TotalStates { get; set; }

    [Column("x")]
    [DefaultValue(0)]
    public required int X { get; set; }

    [Column("y")]
    [DefaultValue(1)]
    public required int Y { get; set; }

    [Column("z", TypeName = "double(10,3)")]
    [DefaultValue(0.0d)]
    public required double Z { get; set; }

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
    [DefaultValue(FurniUsagePolicy.Controller)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public FurniUsagePolicy UsagePolicy { get; set; }

    [Column("extra_data")]
    public string? ExtraData { get; set; }

    public List<FurnitureEntity>? Furnitures { get; set; }
}
