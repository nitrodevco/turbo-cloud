using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

/// <summary>
/// One add-on a shared variable passes on: a subvariable creator or a variable fx standing on
/// its stack, as saved there. A room importing it runs a copy of it on its own reference box.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record SharedWiredVariableAddonSnapshot
{
    /// <summary>The add-on's furni id in the room the variable lives in.</summary>
    [Id(0)]
    public required int BoxId { get; init; }

    /// <summary>The logic the add-on runs, by the name its definition gives it.</summary>
    [Id(1)]
    public required string LogicType { get; init; }

    [Id(2)]
    public required WiredVariableAddonCategories Category { get; init; }

    [Id(3)]
    public ImmutableArray<int> IntParams { get; init; } = [];

    [Id(4)]
    public string StringParam { get; init; } = string.Empty;

    [Id(5)]
    public ImmutableArray<string> VariableIds { get; init; } = [];
}
