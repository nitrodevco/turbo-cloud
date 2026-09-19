namespace Turbo.Inventory.Grains.Modules;

/// <summary>A pet purchase that passed validation and only needs creating.</summary>
internal sealed record PetProductGrant(
    string Name,
    int TypeId,
    int PaletteId,
    int BreedId,
    string Color,
    int RarityLevel
);
