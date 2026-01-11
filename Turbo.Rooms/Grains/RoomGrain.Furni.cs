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
        FurniModule.GetAllOwnersAsync(ct);

    private async Task FlushDirtyItemsAsync(CancellationToken ct)
    {
        if (_state.DirtyFloorItemIds.Count == 0 && _state.DirtyWallItemIds.Count == 0)
            return;

        var batch = new List<RoomItemSnapshot>();

        batch.AddRange(
            _state
                .DirtyFloorItemIds.Select(x =>
                    _state.FloorItemsById.TryGetValue(x, out var item)
                        ? (RoomItemSnapshot)item.GetSnapshot()
                        : null
                )
                .Where(x => x is not null)!
        );

        _state.DirtyFloorItemIds.Clear();

        batch.AddRange(
            _state
                .DirtyWallItemIds.Select(x =>
                    _state.WallItemsById.TryGetValue(x, out var item)
                        ? (RoomItemSnapshot)item.GetSnapshot()
                        : null
                )
                .Where(x => x is not null)!
        );

        _state.DirtyWallItemIds.Clear();

        await _grainFactory
            .GetRoomPersistenceGrain(_state.RoomId)
            .EnqueueDirtyItemsAsync(_state.RoomId, batch, ct);
    }
}
