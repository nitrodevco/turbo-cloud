using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

[GenerateSerializer, Immutable]
public sealed record WiredVariablesSnapshot
{
    [Id(0)]
    public required long AllVariablesHash { get; init; }

    [Id(1)]
    public required List<WiredVariableSnapshot> Variables { get; init; }
}
