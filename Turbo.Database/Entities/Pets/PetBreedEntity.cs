using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Pets;

/// <summary>
/// A sellable palette of a pet type: what the catalog offers and what breeding yields. The
/// palette is the figure's colour variant; several palettes may share a breed (a horse's body
/// shape), which is what the info stand names.
/// </summary>
[Table("pet_breeds")]
[Index(nameof(TypeId), nameof(PaletteId), IsUnique = true)]
public class PetBreedEntity : TurboEntity
{
    [Column("type_id")]
    public required int TypeId { get; set; }

    [Column("palette_id")]
    public required int PaletteId { get; set; }

    [Column("breed_id")]
    public required int BreedId { get; set; }

    [Column("rarity_level")]
    [DefaultValue(0)]
    public int RarityLevel { get; set; }

    [Column("sellable")]
    [DefaultValue(true)]
    public bool Sellable { get; set; } = true;

    [Column("rare")]
    [DefaultValue(false)]
    public bool Rare { get; set; }

    /// <summary>The asset's colour tag of the palette; -1 when it carries none.</summary>
    [Column("color_tag")]
    [DefaultValue(-1)]
    public int ColorTag { get; set; } = -1;
}
