using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public int ToIdx(int x, int y) => _mapModule.ToIdx(x, y);

    public void ComputeTile(int x, int y) => _mapModule.ComputeTile(x, y);

    public void ComputeTile(int id) => _mapModule.ComputeTile(id);

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(int x, int y, CancellationToken ct) =>
        _mapModule.GetTileSnapshotAsync(x, y, ct);

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(int id, CancellationToken ct) =>
        _mapModule.GetTileSnapshotAsync(id, ct);

    public Task<RoomMapSnapshot> GetMapSnapshotAsync(CancellationToken ct) =>
        Task.FromResult(_mapModule.GetMapSnapshot(ct));

    private async Task FlushDirtyTilesAsync(CancellationToken ct)
    {
        if (_liveState.DirtyHeightTileIds.Count == 0)
            return;

        var dirtyHeightTileIds = _liveState.DirtyHeightTileIds;
        _liveState.DirtyHeightTileIds = [];

        var heights = new List<(int X, int Y, short Height)>(
            Math.Min(dirtyHeightTileIds.Count, _roomConfig.MaxTileHeightsPerFlush)
        );

        foreach (var x in dirtyHeightTileIds)
        {
            heights.Add((_mapModule.GetX(x), _mapModule.GetY(x), _liveState.TileEncodedHeights[x]));

            if (heights.Count == heights.Capacity)
            {
                _ = SendComposerToRoomAsync(
                    new HeightMapUpdateMessageComposer { TileHeights = [.. heights] }
                );

                heights.Clear();
            }
        }

        if (heights.Count > 0)
        {
            _ = SendComposerToRoomAsync(
                new HeightMapUpdateMessageComposer { TileHeights = [.. heights] }
            );
        }
    }
}
