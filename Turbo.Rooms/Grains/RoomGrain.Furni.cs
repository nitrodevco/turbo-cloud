using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<bool> AddItemAsync(IRoomItem item, CancellationToken ct)
    {
        try
        {
            if (!await ActionModule.AddItemAsync(item, ct))
                return false;

            return true;
        }
        catch
        {
            // TODO handle exceptions

            return false;
        }
    }

    public async Task<bool> RemoveItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct
    )
    {
        try
        {
            if (!await ActionModule.RemoveItemByIdAsync(ctx, itemId, ct))
                return false;

            return true;
        }
        catch
        {
            // TODO handle exceptions

            return false;
        }
    }

    public async Task<bool> UseItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        try
        {
            if (!await ActionModule.UseItemByIdAsync(ctx, itemId, ct, param))
                return false;

            return true;
        }
        catch
        {
            // TODO handle exceptions

            return false;
        }
    }

    public async Task<bool> ClickItemByIdAsync(
        ActionContext ctx,
        RoomObjectId itemId,
        CancellationToken ct,
        int param = -1
    )
    {
        try
        {
            if (!await ActionModule.ClickItemByIdAsync(ctx, itemId, ct, param))
                return false;

            return true;
        }
        catch
        {
            // TODO handle exceptions

            return false;
        }
    }

    public Task<ImmutableDictionary<PlayerId, string>> GetAllOwnersAsync(CancellationToken ct) =>
        FurniModule.GetAllOwnersAsync(ct);

    public Task<RoomItemSnapshot?> GetItemSnapshotByIdAsync(
        RoomObjectId itemId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state.ItemsById.TryGetValue(itemId, out var item) ? item.GetSnapshot() : null
        );

    private async Task FlushDirtyItemsAsync(CancellationToken ct)
    {
        if (_state.DirtyItemIds.Count == 0)
            return;

        var batch = new List<RoomItemSnapshot>();

        batch.AddRange(
            _state
                .DirtyItemIds.Select(x =>
                    _state.ItemsById.TryGetValue(x, out var item) ? item.GetSnapshot() : null
                )
                .Where(x => x is not null)!
        );

        _state.DirtyItemIds.Clear();

        await _grainFactory
            .GetRoomPersistenceGrain(_state.RoomId)
            .EnqueueDirtyItemsAsync(_state.RoomId, batch, ct);
    }
}
