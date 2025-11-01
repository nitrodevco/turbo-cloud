using Orleans;
using Turbo.Contracts.Enums.Navigator;

namespace Turbo.Primitives.Snapshots.Rooms;

[GenerateSerializer, Immutable]
public record RoomSnapshot
{
    [Id(0)]
    public required long RoomId { get; init; }

    [Id(1)]
    public required string Name { get; init; } = string.Empty;

    [Id(2)]
    public required string Description { get; init; } = string.Empty;

    [Id(3)]
    public required int OwnerId { get; init; }

    [Id(4)]
    public required DoorModeType DoorMode { get; init; }

    [Id(5)]
    public string? Password { get; init; }

    [Id(6)]
    public int ModelId { get; init; }

    [Id(7)]
    public int CategoryId { get; init; }
}
