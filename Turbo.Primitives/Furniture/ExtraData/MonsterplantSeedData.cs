namespace Turbo.Primitives.Furniture.ExtraData;

/// <summary>
/// What a monsterplant seed can grow into, under <see cref="SECTION"/> in the item's or the
/// definition's extra data. Absent, any breed of the type may sprout.
/// </summary>
public sealed record MonsterplantSeedData
{
    public const string SECTION = "monsterplant_seed";

    /// <summary>Lowest rarity category the seed can yield; a rare seed skips the common ones.</summary>
    public int MinRarityLevel { get; init; } = 0;
}
