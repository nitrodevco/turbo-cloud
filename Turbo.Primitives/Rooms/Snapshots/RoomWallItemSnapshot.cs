using System.Text.Json;
using Orleans;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Primitives.Rooms.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.StuffData;

namespace Turbo.Primitives.Rooms.Snapshots;

[GenerateSerializer, Immutable]
public sealed record RoomWallItemSnapshot
{
    [Id(0)]
    public required long Id { get; init; }

    [Id(1)]
    public required long OwnerId { get; init; }

    [Id(2)]
    public required string OwnerName { get; init; }

    [Id(3)]
    public required int SpriteId { get; init; }

    [Id(4)]
    public required string WallLocation { get; init; }

    [Id(5)]
    public required string StuffData { get; init; }

    [Id(6)]
    public required FurniUsagePolicy UsagePolicy { get; init; }
}
