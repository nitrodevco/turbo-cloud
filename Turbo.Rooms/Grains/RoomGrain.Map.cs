using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public int ToIdx(int x, int y) => MapModule.ToIdx(x, y);

    public void ComputeTile(int x, int y) => MapModule.ComputeTile(x, y);

    public void ComputeTile(int id) => MapModule.ComputeTile(id);

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(int x, int y, CancellationToken ct) =>
        MapModule.GetTileSnapshotAsync(x, y, ct);

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(int id, CancellationToken ct) =>
        MapModule.GetTileSnapshotAsync(id, ct);

    public Task<RoomMapSnapshot> GetMapSnapshotAsync(CancellationToken ct) =>
        Task.FromResult(MapModule.GetMapSnapshot(ct));

    private async Task FlushDirtyTilesAsync(CancellationToken ct)
    {
        if (_state.DirtyHeightTileIds.Count == 0)
            return;

        var dirtyHeightTileIds = _state.DirtyHeightTileIds;
        _state.DirtyHeightTileIds = [];

        var heights = new List<(int X, int Y, short Height)>(
            Math.Min(dirtyHeightTileIds.Count, _roomConfig.MaxTileHeightsPerFlush)
        );

        foreach (var x in dirtyHeightTileIds)
        {
            heights.Add((MapModule.GetX(x), MapModule.GetY(x), _state.TileEncodedHeights[x]));

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
