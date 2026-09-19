namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// What a pet customization item (a horse hair style or dye) applies, kept under
/// <see cref="SECTION"/> in the item's extra data or, as the default for that furniture type,
/// in the definition's extra data. Using it replaces the pet's custom part on the same layer.
/// </summary>
public sealed record PetCustomPartData
{
    public const string SECTION = "pet_custom_part";

    /// <summary>Pet types the part fits; empty means any.</summary>
    public int[] PetTypeIds { get; init; } = [];
    public required int LayerId { get; init; }
    public required int PartId { get; init; }
    public required int PaletteId { get; init; }
}
