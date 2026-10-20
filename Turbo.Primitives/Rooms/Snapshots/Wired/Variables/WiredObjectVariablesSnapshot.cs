using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

/// <summary>
/// The variables one furni or user holds, as the wired menu's inspection tab asks for them, and
/// whether that target is in the room at all (a picked up furni is not, and holds nothing).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record WiredObjectVariablesSnapshot
{
    [Id(0)]
    public required bool TargetPresent { get; init; }

    [Id(1)]
    public required List<(WiredVariableId id, WiredVariableValue value)> Values { get; init; }
}
