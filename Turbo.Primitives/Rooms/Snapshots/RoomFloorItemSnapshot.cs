using System.Text.Json;
using Orleans;
using Turbo.Contracts.Enums.Furniture;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Rooms.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.StuffData;

namespace Turbo.Primitives.Rooms.Snapshots;

[GenerateSerializer, Immutable]
public sealed record RoomFloorItemSnapshot
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
    public required int X { get; init; }

    [Id(5)]
    public required int Y { get; init; }

    [Id(6)]
    public required double Z { get; init; }

    [Id(7)]
    public required Rotation Rotation { get; init; }

    [Id(8)]
    public required double StackHeight { get; init; }

    [Id(9)]
    public required StuffDataSnapshot StuffData { get; init; }

    [Id(10)]
    public required string StuffDataJson { get; init; }

    [Id(11)]
    public required FurniUsagePolicy UsagePolicy { get; init; }

    public static RoomFloorItemSnapshot FromFloorItem(IRoomFloorItem item)
    {
        var stuffData = StuffDataSnapshot.FromStuffData(item.Logic.StuffData);

        return new RoomFloorItemSnapshot
        {
            Id = item.Id,
            OwnerId = item.OwnerId,
            OwnerName = item.OwnerName,
            SpriteId = item.Definition.SpriteId,
            X = item.X,
            Y = item.Y,
            Z = item.Z,
            Rotation = item.Rotation,
            StackHeight = item.Definition.StackHeight,
            StuffData = stuffData,
            StuffDataJson = JsonSerializer.Serialize(stuffData, stuffData.GetType()),
            UsagePolicy = item.Logic.GetUsagePolicy(),
        };
    }
}
