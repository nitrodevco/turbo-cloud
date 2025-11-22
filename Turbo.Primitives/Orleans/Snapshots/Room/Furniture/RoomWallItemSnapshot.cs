using System.Text.Json;
using Orleans;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Orleans.Snapshots.Room.StuffData;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Primitives.Rooms.Furniture.Wall;

namespace Turbo.Primitives.Orleans.Snapshots.Room.Furniture;

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
    public required StuffDataSnapshot StuffData { get; init; }

    [Id(6)]
    public required string StuffDataJson { get; init; }

    [Id(7)]
    public required FurniUsagePolicy UsagePolicy { get; init; }

    public static RoomWallItemSnapshot FromWallItem(IRoomWallItem item)
    {
        var stuffData = item.Logic.StuffData;

        return new RoomWallItemSnapshot
        {
            Id = item.Id,
            OwnerId = item.OwnerId,
            OwnerName = item.OwnerName,
            SpriteId = item.Definition.SpriteId,
            WallLocation = item.WallLocation,
            StuffData = StuffDataSnapshot.FromStuffData(stuffData),
            StuffDataJson = JsonSerializer.Serialize(stuffData, stuffData.GetType()),
            UsagePolicy = item.Logic.GetUsagePolicy(),
        };
    }
}
