using Orleans;
using Turbo.Contracts.Enums.Rooms.Object;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Snapshots.Rooms.StuffData;

namespace Turbo.Primitives.Snapshots.Rooms;

[GenerateSerializer, Immutable]
public sealed record RoomFloorItemSnapshot
{
    [Id(0)]
    public required long Id { get; init; }

    [Id(1)]
    public required long OwnerId { get; init; }

    [Id(2)]
    public required int SpriteId { get; init; }

    [Id(3)]
    public required int X { get; init; }

    [Id(4)]
    public required int Y { get; init; }

    [Id(5)]
    public required float Z { get; init; }

    [Id(6)]
    public required Rotation Rotation { get; init; }

    [Id(7)]
    public required float StackHeight { get; init; }

    [Id(8)]
    public required StuffDataSnapshot StuffData { get; init; }

    public static RoomFloorItemSnapshot FromFloorItem(IRoomFloorItem item)
    {
        return new RoomFloorItemSnapshot
        {
            Id = item.Id,
            OwnerId = item.OwnerId,
            SpriteId = item.Definition.SpriteId,
            X = item.X,
            Y = item.Y,
            Z = item.Z,
            Rotation = item.Rotation,
            StackHeight = item.Definition.Z,
            StuffData = StuffDataSnapshot.FromStuffData(item.StuffData),
        };
    }
}
