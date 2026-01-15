using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

[GenerateSerializer, Immutable]
public sealed record WiredVariablesSnapshot
{
    [Id(0)]
    public required int GlobalHash { get; init; }

    [Id(1)]
    public required List<WiredVariableSnapshot> Variables { get; init; }
}
