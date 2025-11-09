using Orleans;

namespace Turbo.Primitives.Snapshots.Rooms;

[GenerateSerializer, Immutable]
public sealed record RoomChangedSnapshot
{
    [Id(0)]
    public required long PreviousRoomId { get; init; }

    [Id(1)]
    public required long CurrentRoomId { get; init; }

    [Id(2)]
    public required bool Changed { get; init; }
}
