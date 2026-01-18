using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

[GenerateSerializer, Immutable]
public record WiredVariableInfoAndHoldersSnapshot : WiredVariableContextSnapshot
{
    [Id(1)]
    public required WiredVariableSnapshot Variable { get; init; }

    [Id(2)]
    public required List<(int objectId, int value)> Holders { get; init; }
}
