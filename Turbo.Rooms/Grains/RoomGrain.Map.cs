using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Mapping;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public int ToIdx(int x, int y) => MapModule.ToIdx(x, y);

    public void ComputeTile(int x, int y) => MapModule.ComputeTile(x, y);

    public void ComputeTile(int id) => MapModule.ComputeTile(id);

    public Task ClickTileAsync(ActionContext ctx, int x, int y, CancellationToken ct) =>
        MapModule.ClickTileAsync(ctx, x, y, ct);

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(int x, int y, CancellationToken ct) =>
        MapModule.GetTileSnapshotAsync(x, y, ct);

    public Task<RoomTileSnapshot> GetTileSnapshotAsync(int id, CancellationToken ct) =>
        MapModule.GetTileSnapshotAsync(id, ct);

    public Task<RoomMapSnapshot> GetMapSnapshotAsync(CancellationToken ct) =>
        Task.FromResult(MapModule.GetMapSnapshot(ct));

    public Task<ImmutableArray<RoomTilePositionSnapshot>> GetOccupiedTilesAsync(
        CancellationToken ct
    )
    {
        var tiles = ImmutableArray.CreateBuilder<RoomTilePositionSnapshot>();
        var flags = _state.TileFlags;

        for (var idx = 0; idx < flags.Length; idx++)
        {
            if (!flags[idx].Has(RoomTileFlags.FurnitureOccupied))
                continue;

            var (x, y) = MapModule.GetTileXY(idx);

            tiles.Add(new RoomTilePositionSnapshot { X = x, Y = y });
        }

        return Task.FromResult(tiles.ToImmutable());
    }

    private Task FlushDirtyTilesAsync(CancellationToken ct)
    {
        if (_state.DirtyHeightTileIds.Count == 0)
            return Task.CompletedTask;
        var dirtyHeightTileIds = _state.DirtyHeightTileIds;
        var sentValues = _state.DirtyHeightSentValues;
        _state.DirtyHeightTileIds = [];
        _state.DirtyHeightSentValues = [];

        var heights = new List<(int X, int Y, short Height)>(
            Math.Min(dirtyHeightTileIds.Count, _roomConfig.MaxTileHeightsPerFlush)
        );

        // In tile order, as Habbo lists them.
        foreach (var x in dirtyHeightTileIds.Order())
        {
            // Back where it was when last sent: the client already has it.
            if (sentValues.TryGetValue(x, out var sent) && sent == _state.TileEncodedHeights[x])
                continue;

            heights.Add((MapModule.GetX(x), MapModule.GetY(x), _state.TileEncodedHeights[x]));

            if (heights.Count == heights.Capacity)
            {
                SendComposerToRoomAndForget(
                    new HeightMapUpdateMessageComposer { TileHeights = [.. heights] }
                );

                heights.Clear();
            }
        }

        if (heights.Count > 0)
        {
            SendComposerToRoomAndForget(
                new HeightMapUpdateMessageComposer { TileHeights = [.. heights] }
            );
        }

        return Task.CompletedTask;
    }
}
