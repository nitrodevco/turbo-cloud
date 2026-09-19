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

    public static bool IsMonsterplant(int typeId) => typeId == MONSTERPLANT;

    public static bool CanNestBreed(int typeId) => Array.IndexOf(NEST_BREEDABLE, typeId) >= 0;
}
