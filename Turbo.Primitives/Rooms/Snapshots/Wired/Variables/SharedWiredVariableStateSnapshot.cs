using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

/// <summary>
/// A shared variable as the room it lives in holds it: its description and every value, by
/// holder. A user variable's holder is the player id, a global's is 0.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record SharedWiredVariableStateSnapshot
{
    [Id(0)]
    public required WiredVariableSnapshot Variable { get; init; }

    [Id(1)]
    public required ImmutableDictionary<int, long> Values { get; init; }
}
