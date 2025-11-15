using Orleans;

namespace Turbo.Primitives.Orleans.Snapshots.Room;

[GenerateSerializer, Immutable]
public sealed record RoomChangedInfoSnapshot
{
    [Id(0)]
    public required long PreviousRoomId { get; init; }

    [Id(1)]
    public required long CurrentRoomId { get; init; }

    [Id(2)]
    public required bool Changed { get; init; }
}
