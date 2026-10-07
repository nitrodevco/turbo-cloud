using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Gamedata;

/// <summary>
/// One of the hotel's products, its product data: the name and description the client shows for a
/// catalog offer whose name key (<c>catalog_offers.localization_id</c>) is the code. Habbo's taken
/// in, and the hotel's own.
/// </summary>
[Table("gamedata_products")]
[Index(nameof(Code), IsUnique = true)]
public class GamedataProductEntity : TurboEntity
{
    public const int CODE_MAX_LENGTH = 255;

    /// <summary>Binary, as a text key's: Habbo has codes that differ only in case.</summary>
    public const string CODE_COLLATION = "utf8mb4_bin";

    [Column("code")]
    [MaxLength(CODE_MAX_LENGTH)]
    public required string Code { get; set; }

    [Column("name", TypeName = "longtext")]
    public string? Name { get; set; }

    [Column("description", TypeName = "longtext")]
    public string? Description { get; set; }
}
