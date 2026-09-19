namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// What a pet package holds, kept in the item's extra data under <see cref="SECTION"/> (or, as
/// the default for every package of that furniture type, in the definition's extra data).
/// </summary>
public sealed record PetPackageData
{
    public const string SECTION = "pet";

    public required int TypeId { get; init; }
    public required int PaletteId { get; init; }
    public required string Color { get; init; }
}
