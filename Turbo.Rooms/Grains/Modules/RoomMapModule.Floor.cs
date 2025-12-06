using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomMapModule
{
    public bool AddFloorItem(IRoomFloorItem item, bool flush, out List<int> updatedTileIds)
    {
        var tileIdx = ToIdx(item.X, item.Y);

        if (!InBounds(tileIdx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        updatedTileIds = [];

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
                _state.TileFloorStacks[idx].Add(item.ObjectId.Value);

                ComputeTile(idx);

                updatedTileIds.Add(idx);
            }
        }

        if (flush)
            _ = _roomGrain.SendComposerToRoomAsync(item.GetAddComposer(), CancellationToken.None);

        return true;
    }

    public bool PlaceFloorItem(
        IRoomFloorItem item,
        int nTileIdx,
        Rotation rot,
        bool flush,
        out List<int> updatedTileIds
    )
    {
        if (!InBounds(nTileIdx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        item.SetPosition(GetX(nTileIdx), GetY(nTileIdx), _state.TileHeights[nTileIdx]);
        item.SetRotation(rot);

        return AddFloorItem(item, flush, out updatedTileIds);
    }

    public bool MoveFloorItem(
        IRoomFloorItem item,
        int tileIdx,
        Rotation rot,
        bool flush,
        out List<int> updatedTileIds
    )
    {
        if (!InBounds(tileIdx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        updatedTileIds = [];

        RemoveFloorItem(item, -1, false, out var removedTileIds);
        updatedTileIds.AddRange(removedTileIds);

        item.SetPosition(GetX(tileIdx), GetY(tileIdx), _state.TileHeights[tileIdx]);
        item.SetRotation(rot);

        AddFloorItem(item, false, out var addedTileIds);
        updatedTileIds.AddRange(addedTileIds);

        updatedTileIds = [.. updatedTileIds.Distinct()];

        if (flush)
            _ = _roomGrain.SendComposerToRoomAsync(
                item.GetUpdateComposer(),
                CancellationToken.None
            );

        return true;
    }

    public bool RemoveFloorItem(
        IRoomFloorItem item,
        long pickerId,
        bool flush,
        out List<int> updatedTileIds
    )
    {
        var tileIdx = ToIdx(item.X, item.Y);

        if (!InBounds(tileIdx))
            throw new TurboException(TurboErrorCodeEnum.TileOutOfBounds);

        updatedTileIds = [];

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
                _state.TileFloorStacks[idx].Remove(item.ObjectId.Value);

                ComputeTile(idx);

                updatedTileIds.Add(idx);
            }
        }

        if (flush)
            _ = _roomGrain.SendComposerToRoomAsync(
                item.GetRemoveComposer(pickerId),
                CancellationToken.None
            );

        return true;
    }
}
