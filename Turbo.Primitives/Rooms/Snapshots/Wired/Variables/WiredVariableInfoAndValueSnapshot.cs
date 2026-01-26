using Orleans;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

[GenerateSerializer, Immutable]
public record WiredVariableInfoAndValueSnapshot : WiredVariableContextSnapshot
{
    [Id(1)]
    public required WiredVariableSnapshot Variable { get; init; }

    [Id(2)]
    public required WiredVariableValue Value { get; init; }
}
