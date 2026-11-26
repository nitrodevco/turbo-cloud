using System;

namespace Turbo.Primitives.Pets;

/// <summary>
/// Pet type ids the client treats specially. Type ids index the client's <c>pet.configuration</c>
/// list, so they are protocol values rather than data.
/// </summary>
public static class PetTypes
{
    public const int DOG = 0;
    public const int CAT = 1;
    public const int TERRIER = 3;
    public const int BEAR = 4;
    public const int PIG = 5;
    public const int HORSE = 15;
    public const int MONSTERPLANT = 16;

    /// <summary>Pet types whose menu offers nest breeding.</summary>
    public static readonly int[] NEST_BREEDABLE = [DOG, CAT, TERRIER, BEAR, PIG];

    /// <summary>The rarity level a pet with none is sent with.</summary>
    public const int NO_RARITY_LEVEL = -1;

    public static bool IsMonsterplant(int typeId) => typeId == MONSTERPLANT;

    /// <summary>
    /// The rarity level a pet's inventory row tells the client: a monster plant's own, none for
    /// any other pet. The client draws the rarity plaque on a pet's thumb for any level of 0 or
    /// more (<c>PetsGridItem.updateRarityOverlay</c>), and only monster plants have one: the pets
    /// inventory's rarity filter lists plants alone (<c>PetsView</c>). Another pet's stored level is
    /// its palette's nest-breeding category, not a rarity the client shows.
    /// </summary>
    public static int InventoryRarityLevel(int typeId, int rarityLevel) =>
        IsMonsterplant(typeId) ? rarityLevel : NO_RARITY_LEVEL;

    public static bool CanNestBreed(int typeId) => Array.IndexOf(NEST_BREEDABLE, typeId) >= 0;
}
