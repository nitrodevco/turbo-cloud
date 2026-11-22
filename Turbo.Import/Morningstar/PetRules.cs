using System.Collections.Generic;

namespace Turbo.Import.Morningstar;

/// <summary>The part of the target's pet configuration (<c>PetConfig</c>) an import needs.</summary>
public sealed record PetRules
{
    /// <summary>Experience to reach level 2, 3, ... in order.</summary>
    public required IReadOnlyList<int> LevelExperience { get; init; }

    public required int MaxLevel { get; init; }

    public required int MonsterplantMaxLevel { get; init; }

    /// <summary>Seconds a monsterplant takes to grow a level.</summary>
    public required int MonsterplantGrowthSeconds { get; init; }

    /// <summary>A grown monsterplant's wellbeing when watered, in seconds.</summary>
    public required int MonsterplantWellBeingSeconds { get; init; }

    /// <summary>A growing monsterplant's wellbeing when watered, in seconds.</summary>
    public required int MonsterplantBabyWellBeingSeconds { get; init; }
}
