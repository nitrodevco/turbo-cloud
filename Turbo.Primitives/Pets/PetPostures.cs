using System.Globalization;

namespace Turbo.Primitives.Pets;

/// <summary>
/// Posture tokens the client resolves against a pet's visualization. A monsterplant's growth
/// stage is the posture <c>grw&lt;level&gt;</c> until it is fully grown, then <c>std</c>.
/// </summary>
public static class PetPostures
{
    public const string STAND = "std";
    public const string GROW_PREFIX = "grw";

    /// <summary>Level at which a monsterplant stops growing and shows as <see cref="STAND"/>.</summary>
    public const int MONSTERPLANT_GROWN_LEVEL = 7;

    public static string ForMonsterplant(int level) =>
        level >= MONSTERPLANT_GROWN_LEVEL
            ? STAND
            : GROW_PREFIX + level.ToString(CultureInfo.InvariantCulture);
}
