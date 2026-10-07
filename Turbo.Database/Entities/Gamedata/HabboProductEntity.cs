using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// One of Habbo's products as it was last taken in. The next import compares against it to tell
/// what Habbo changed from what the hotel changed.
/// </summary>
[Table("habbo_products")]
[Index(nameof(Code), IsUnique = true)]
public class HabboProductEntity : TurboEntity
{
    [Column("code")]
    [MaxLength(GamedataProductEntity.CODE_MAX_LENGTH)]
    public required string Code { get; set; }

    [Column("name", TypeName = "longtext")]
    public string? Name { get; set; }

    [Column("description", TypeName = "longtext")]
    public string? Description { get; set; }

    /// <summary>The version it was last taken in from.</summary>
    [Column("version_id")]
    public required int HabboProductVersionEntityId { get; set; }
}
