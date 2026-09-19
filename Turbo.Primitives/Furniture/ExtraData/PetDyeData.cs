namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// What a body dye applies, under <see cref="SECTION"/> in the item's or the definition's
/// extra data: the pet keeps its breed and switches to the palette of that breed carrying the
/// colour tag (a horse dye recolours the coat without changing the body shape).
/// </summary>
public sealed record PetDyeData
{
    public const string SECTION = "pet_dye";

    /// <summary>Pet types the dye fits; empty means any.</summary>
    public int[] PetTypeIds { get; init; } = [];
    public required int ColorTag { get; init; }
}
