using System.Collections.Generic;

namespace Turbo.Import.Morningstar;

/// <summary>What an import of a Morningstar hotel copies, and how.</summary>
public sealed record MorningstarImportOptions
{
    /// <summary>Read and translate everything, write nothing.</summary>
    public bool DryRun { get; init; }

    /// <summary>Also copy the catalog's pages and offers, beside the ones the target has.</summary>
    public bool Catalog { get; init; }

    /// <summary>Also copy room chat logs and delivered private messages.</summary>
    public bool Chatlogs { get; init; }

    /// <summary>
    /// The permission group each Morningstar rank goes in, by rank id. A rank not named here is
    /// matched by its name (<c>Administrator</c> to <c>admin</c>, <c>Moderator</c> to
    /// <c>moderator</c>...); rank 1, and a rank nothing matches, is the default group.
    /// </summary>
    public IReadOnlyDictionary<int, string> RankGroups { get; init; } =
        new Dictionary<int, string>();

    /// <summary>
    /// How the target levels pets (<c>Turbo:Pets</c>): Morningstar stores a pet's experience and
    /// works its level out, Turbo stores both, so the level is worked out here by the target's
    /// rules, which the host reads from its configuration.
    /// </summary>
    public required PetRules Pets { get; init; }

    /// <summary>
    /// The furni logic keys the target registers (<c>[RoomObjectLogic]</c>): a definition the
    /// import creates gets one of these, never a key the server would not find.
    /// </summary>
    public required IReadOnlySet<string> LogicKeys { get; init; }
}

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
