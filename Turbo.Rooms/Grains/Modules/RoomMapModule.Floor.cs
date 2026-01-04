using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomMapModule
{
    public bool AddFloorItem(IRoomFloorItem item)
    {
        var tileIdx = ToIdx(item.X, item.Y);

        if (!InBounds(tileIdx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        if (
            GetTileIdForSize(
                item.X,
                item.Y,
                item.Rotation,
                item.Definition.Width,
                item.Definition.Length,
                out var tileIds
            )
        )
        {
            foreach (var idx in tileIds)
            {
                _state.TileFloorStacks[idx].Add(item.ObjectId);

                ComputeTile(idx);
            }
        }

        return true;
    }

    public bool PlaceFloorItem(IRoomFloorItem item, int nTileIdx, Rotation rot)
    {
        if (!InBounds(nTileIdx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        item.SetPosition(GetX(nTileIdx), GetY(nTileIdx), _state.TileHeights[nTileIdx]);
        item.SetRotation(rot);

        return AddFloorItem(item);
    }

    public bool MoveFloorItem(IRoomFloorItem item, int tileIdx, Rotation rot)
    {
        if (!InBounds(tileIdx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        RemoveFloorItem(item);

        item.SetPosition(GetX(tileIdx), GetY(tileIdx), _state.TileHeights[tileIdx]);
        item.SetRotation(rot);

        AddFloorItem(item);

        return true;
    }

    public bool RollFloorItem(IRoomFloorItem item, int tileIdx, double z)
    {
        if (!InBounds(tileIdx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        RemoveFloorItem(item);

        item.SetPosition(GetX(tileIdx), GetY(tileIdx), z);

        AddFloorItem(item);

        return true;
    }

    public bool RemoveFloorItem(IRoomFloorItem item)
    {
        var tileIdx = ToIdx(item.X, item.Y);

        if (!InBounds(tileIdx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        if (
            GetTileIdForSize(
                item.X,
                item.Y,
                item.Rotation,
                item.Definition.Width,
                item.Definition.Length,
                out var tileIds
            )
        )
        {
            foreach (var idx in tileIds)
            {
                _state.TileFloorStacks[idx].Remove(item.ObjectId);

                ComputeTile(idx);
            }
        }

        return true;
    }
}
