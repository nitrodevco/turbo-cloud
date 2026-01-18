using Orleans;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

[GenerateSerializer, Immutable]
public record WiredVariableAllInRoomSnapshot : WiredVariableContextSnapshot
{
    [Id(1)]
    public required WiredVariableHash AllVariablesHash { get; init; }
}
