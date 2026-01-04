using System.Collections.Generic;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

[GenerateSerializer, Immutable]
public sealed record WiredContextSnapshot
{
    [Id(0)]
    public required Dictionary<string, object?> Variables { get; init; }

    [Id(1)]
    public required WiredSelectionSetSnapshot Selected { get; init; }
}
