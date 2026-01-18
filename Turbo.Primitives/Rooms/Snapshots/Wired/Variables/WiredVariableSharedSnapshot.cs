using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired.Variables;

[GenerateSerializer, Immutable]
public record WiredVariableSharedSnapshot : WiredVariableContextSnapshot
{
    [Id(1)]
    public required WiredVariableSnapshot Variable { get; init; }

    [Id(2)]
    public required int RoomId { get; init; }

    [Id(3)]
    public required string RoomName { get; init; }
}
