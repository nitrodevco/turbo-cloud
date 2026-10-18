namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// Which monsterplants a re-breeding potion fits, under <see cref="SECTION"/> in the item's or
/// the definition's extra data: Habbo sells one potion per rarity band ("Rebreeding Potion 2
/// (One-time use for a single 6-8 level plant)"). Absent, the potion fits any plant.
/// </summary>
public sealed record PetRebreedData
{
    public const string SECTION = "pet_rebreed";

    public int MinRarityLevel { get; init; } = 0;
    public int MaxRarityLevel { get; init; } = int.MaxValue;
}
