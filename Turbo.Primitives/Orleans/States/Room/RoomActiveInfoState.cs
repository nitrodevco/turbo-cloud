using System;
using Orleans;

namespace Turbo.Primitives.Orleans.States.Room;

[GenerateSerializer]
public sealed class RoomActiveInfoState
{
    [Id(0)]
    public required long RoomId { get; set; } = -1;

    [Id(1)]
    public required string Name { get; set; } = string.Empty;

    [Id(2)]
    public required string Description { get; set; } = string.Empty;

    [Id(3)]
    public required long OwnerId { get; set; } = -1;

    [Id(4)]
    public required string OwnerName { get; set; } = string.Empty;

    [Id(5)]
    public required DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
}
