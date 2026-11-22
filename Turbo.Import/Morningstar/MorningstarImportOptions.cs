using System;
using System.Collections.Generic;
using Turbo.Primitives.Furniture.Snapshots;

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

    /// <summary>
    /// The logic the target's rooms run for a definition: its logic column, or what the room
    /// swaps that for by classname, category or custom params (a game timer, a highscore board, a
    /// wired box...). An item's state is written for this logic.
    /// </summary>
    public required Func<FurnitureDefinitionSnapshot, string> LogicOf { get; init; }

    /// <summary>The target's limits what is copied is held to (<see cref="ImportLimits"/>).</summary>
    public required ImportLimits Limits { get; init; }
}
