using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Gamedata.Enums;

namespace Turbo.Primitives.Gamedata.Snapshots;

/// <summary>
/// A furniture definition as the client's furnidata has it: <see cref="Item"/> is the item's JSON
/// exactly as the built file writes it, its offer ids stamped from the catalog.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record FurnitureDefinitionDetail
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required ProductType ProductType { get; init; }

    [Id(2)]
    public required string ClassName { get; init; }

    [Id(3)]
    public required string Item { get; init; }

    /// <summary>Habbo's item in its newest release, as JSON; null for the hotel's own furniture.</summary>
    [Id(4)]
    public string? Habbo { get; init; }

    /// <summary>
    /// What Habbo's asset file of it said (states, logic, visualization, size, directions, colours),
    /// as JSON - or why it could not be read. Null when it was not read.
    /// </summary>
    [Id(5)]
    public string? HabboFile { get; init; }

    [Id(6)]
    public bool HabboFileRead { get; init; }

    /// <summary>Its <c>total_states</c>, which the file does not carry: furnidata has no states.</summary>
    [Id(7)]
    public required int States { get; init; }

    /// <summary>The bundle it is drawn from: its classname without the <c>*N</c> colour.</summary>
    [Id(8)]
    public required string AssetName { get; init; }

    /// <summary>Its tags, as the list gives them.</summary>
    [Id(9)]
    public required ImmutableArray<FurnitureTag> Tags { get; init; }

    /// <summary>
    /// The server's own columns, which furnidata does not carry, as a JSON object by their keys:
    /// <c>classname</c>, <c>id</c>, <c>logic</c>, <c>usagepolicy</c>, <c>cangroup</c>,
    /// <c>cansell</c>, <c>extradata</c>.
    /// </summary>
    [Id(10)]
    public required string ServerFields { get; init; }

    /// <summary>As <see cref="FurnitureListItem.MatchingLogic"/>.</summary>
    [Id(11)]
    public string? MatchingLogic { get; init; }
}
