using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

[GenerateSerializer, Immutable]
public record WiredVariableAllInRoomSnapshot : WiredVariableContextSnapshot
{
    [Id(1)]
    public required long VariableHash { get; init; }
}
