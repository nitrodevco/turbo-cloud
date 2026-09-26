using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomMapModule
{
    private bool AddFloorItem(IRoomFloorItem item)
    {
        var tileIdx = ToIdx(item.X, item.Y);

        if (!InBounds(tileIdx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        // Placement refuses a footprint off the map, so reaching this is a bug in the caller.
        if (!TryGetTileIds(FloorFootprint.Of(item), out var tileIds))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        foreach (var idx in tileIds)
        {
            _roomGrain._state.TileFloorStacks[idx].Add(item.ObjectId);

            ComputeTile(idx);
        }

        return true;
    }

    public bool MoveFloorItem(
        IRoomFloorItem item,
        int tileIdx,
        Altitude? z = null,
        Rotation? targetRot = null
    )
    {
        if (!InBounds(tileIdx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        var (sourceX, sourceY, sourceRot) = (item.X, item.Y, item.Rotation);
        var (targetX, targetY) = GetTileXY(tileIdx);
        var finalZ =
            z
            ?? (
                sourceX != targetX || sourceY != targetY
                    ? _roomGrain._state.TileHeights[tileIdx]
                    : item.Z
            );
        var finalRot = targetRot ?? sourceRot;

        if (sourceX != targetX || sourceY != targetY || sourceRot != finalRot)
        {
            RemoveFloorItem(item);

            item.SetPosition(targetX, targetY);
            item.SetPositionZ(finalZ);
            item.SetRotation(finalRot);

            AddFloorItem(item);
        }
        else
        {
            item.SetPositionZ(finalZ);

            ComputeTile(tileIdx);
        }

        return true;
    }

    public bool RollFloorItem(IRoomFloorItem item, int tileIdx, Altitude z)
    {
        if (!InBounds(tileIdx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        RemoveFloorItem(item);

        var (targetX, targetY) = GetTileXY(tileIdx);

        item.SetPosition(targetX, targetY);
        item.SetPositionZ(z);

        AddFloorItem(item);

        return true;
    }

    public bool RemoveFloorItem(IRoomFloorItem item)
    {
        var tileIdx = ToIdx(item.X, item.Y);

        if (!InBounds(tileIdx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        if (!TryGetTileIds(FloorFootprint.Of(item), out var tileIds))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        foreach (var idx in tileIds)
        {
            _roomGrain._state.TileFloorStacks[idx].Remove(item.ObjectId);

            ComputeTile(idx);
        }

        return true;
    }
}
