using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public Task<ImmutableDictionary<PlayerId, string>> GetAllOwnersAsync(CancellationToken ct) =>
        _furniModule.GetAllOwnersAsync(ct);

    private async Task FlushDirtyItemsAsync(CancellationToken ct)
    {
        if (_liveState.DirtyFloorItemIds.Count == 0 && _liveState.DirtyWallItemIds.Count == 0)
            return;

        var batch = new List<RoomItemSnapshot>();

        batch.AddRange(
            _liveState
                .DirtyFloorItemIds.Select(x =>
                    _liveState.FloorItemsById.TryGetValue(x, out var item)
                        ? (RoomItemSnapshot)item.GetSnapshot()
                        : null
                )
                .Where(x => x is not null)!
        );

        _liveState.DirtyFloorItemIds.Clear();

        batch.AddRange(
            _liveState
                .DirtyWallItemIds.Select(x =>
                    _liveState.WallItemsById.TryGetValue(x, out var item)
                        ? (RoomItemSnapshot)item.GetSnapshot()
                        : null
                )
                .Where(x => x is not null)!
        );

        _liveState.DirtyWallItemIds.Clear();

        await _grainFactory
            .GetRoomPersistenceGrain(_roomId)
            .EnqueueDirtyItemsAsync(_roomId, batch, ct);
    }
}
