using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

/// <summary>
/// The add-ons a shared variable passes on to the rooms using it: what a share add-on on its
/// stack shares. Only the categories in <see cref="Categories"/> are filled.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record SharedWiredVariableAddonsSnapshot
{
    [Id(0)]
    public required WiredVariableAddonCategories Categories { get; init; }

    /// <summary>Whether a text connector stands on the variable's stack, empty or not.</summary>
    [Id(1)]
    public bool HasTextConnector { get; init; }

    [Id(2)]
    public Dictionary<WiredVariableValue, string> TextConnectors { get; init; } = [];

    [Id(3)]
    public string? TextConnectorFallback { get; init; }
}
