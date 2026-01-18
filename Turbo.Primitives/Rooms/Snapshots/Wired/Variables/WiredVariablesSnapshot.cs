using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

[GenerateSerializer, Immutable]
public sealed record WiredVariablesSnapshot
{
    [Id(0)]
    public required WiredVariableHash AllVariablesHash { get; init; }

    [Id(1)]
    public required List<WiredVariableSnapshot> Variables { get; init; }
}
