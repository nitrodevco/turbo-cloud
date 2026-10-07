using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Furniture.Enums;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// One of Habbo's furniture as it was last taken in: its furnidata item, as Habbo wrote it. The
/// next import compares against it to tell what Habbo changed from what the hotel changed.
/// </summary>
[Table("habbo_furniture")]
[Index(nameof(ProductType), nameof(ClassName), IsUnique = true)]
public class HabboFurnitureEntity : TurboEntity
{
    [Column("type")]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required ProductType ProductType { get; set; }

    [Column("class_name")]
    [MaxLength(128)]
    public required string ClassName { get; set; }

    [Column("sprite_id")]
    public required int SpriteId { get; set; }

    /// <summary>The furnidata item, as JSON.</summary>
    [Column("data", TypeName = "longtext")]
    public required string Data { get; set; }

    /// <summary>The release it was last taken in from.</summary>
    [Column("release_id")]
    public required int HabboReleaseEntityId { get; set; }
}
