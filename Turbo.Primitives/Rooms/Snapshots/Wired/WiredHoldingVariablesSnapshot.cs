using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Rooms.Enums.Wired;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

[GenerateSerializer, Immutable]
public sealed record WiredHoldingVariablesSnapshot
{
    [Id(0)]
    public required WiredVariableTargetType TargetType { get; init; }

    [Id(1)]
    public required int TargetId { get; init; }

    [Id(2)]
    public required List<(long id, int value)> VariableValues { get; init; }
}
