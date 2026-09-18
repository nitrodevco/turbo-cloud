using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomFurniModule(RoomGrain roomGrain)
{
    private readonly RoomGrain _roomGrain = roomGrain;

    public Task<ImmutableDictionary<PlayerId, string>> GetAllOwnersAsync(CancellationToken ct) =>
        Task.FromResult(_roomGrain._state.OwnerNamesById.ToImmutableDictionary());

    internal async Task EnsureFurniLoadedAsync(CancellationToken ct)
    {
        if (_roomGrain._state.IsFurniLoaded)
            return;

        var (floorItems, wallItems, ownerNames) = await _roomGrain._itemsLoader.LoadByRoomIdAsync(
            _roomGrain.RoomId,
            ct
        );

        foreach (var (id, name) in ownerNames)
            _roomGrain._state.OwnerNamesById.TryAdd(id, name);

        _roomGrain._state.IsTileComputationPaused = true;

        foreach (var item in floorItems)
            await _roomGrain.ObjectModule.AttatchObjectAsync(item, ct);

        _roomGrain._state.IsTileComputationPaused = false;

        _roomGrain.MapModule.ComputeAllTiles();
        _roomGrain._state.DirtyHeightTileIds.Clear();

        foreach (var item in wallItems)
            await _roomGrain.ObjectModule.AttatchObjectAsync(item, ct);

        _roomGrain._state.IsFurniLoaded = true;
    }

    public Task<RoomItemSnapshot?> GetItemSnapshotByIdAsync(
        RoomObjectId objectId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _roomGrain._state.ItemsById.TryGetValue(objectId, out var item)
                ? item.GetSnapshot()
                : null
        );
}
