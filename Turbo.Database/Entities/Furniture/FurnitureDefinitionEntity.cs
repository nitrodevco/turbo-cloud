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

    [Column("stack_height", TypeName = "double(10,4)")]
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

    // What only the client's furnidata says of the furniture. The rest of a furnidata item is
    // the columns above (xdim is width, canstandon is can_walk, ...) and its offers the catalog's.

    /// <summary>furnidata <c>revision</c>: the folder the client loads the furniture's asset from.</summary>
    [Column("revision")]
    [DefaultValue(0)]
    public int Revision { get; set; }

    /// <summary>furnidata <c>category</c> (<c>shelf</c>, <c>chair</c>); <see cref="FurniCategory"/> is its <c>specialtype</c>.</summary>
    [Column("client_category")]
    [MaxLength(64)]
    public string? ClientCategory { get; set; }

    [Column("default_dir")]
    [DefaultValue(0)]
    public int DefaultDirection { get; set; }

    /// <summary>The colour of each tinted layer (furnidata <c>partcolors</c>); null for furniture without.</summary>
    [Column("part_colors")]
    public List<string>? PartColors { get; set; }

    /// <summary>The name players see (furnidata <c>name</c>); <see cref="Name"/> is its classname.</summary>
    [Column("public_name")]
    [MaxLength(255)]
    public string? PublicName { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("ad_url")]
    [MaxLength(512)]
    public string? AdUrl { get; set; }

    [Column("custom_params")]
    [MaxLength(512)]
    public string? CustomParams { get; set; }

    [Column("furni_line")]
    [MaxLength(64)]
    public string? FurniLine { get; set; }

    [Column("environment")]
    [MaxLength(64)]
    public string? Environment { get; set; }

    [Column("rare")]
    [DefaultValue(false)]
    public bool Rare { get; set; }

    [Column("excluded_dynamic")]
    [DefaultValue(false)]
    public bool ExcludedDynamic { get; set; }

    public List<FurnitureEntity>? Furnitures { get; set; }
}
