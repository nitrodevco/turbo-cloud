using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;

namespace Turbo.Rooms.Grains.Modules;

public sealed partial class RoomFurniModule(
    RoomGrain roomGrain,
    RoomLiveState roomLiveState,
    RoomMapModule roomMapModule
) : IRoomModule
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomLiveState _state = roomLiveState;
    private readonly RoomMapModule _roomMap = roomMapModule;

    public Task<ImmutableDictionary<PlayerId, string>> GetAllOwnersAsync(CancellationToken ct) =>
        Task.FromResult(_state.OwnerNamesById.ToImmutableDictionary());

    internal async Task EnsureFurniLoadedAsync(CancellationToken ct)
    {
        if (_state.IsFurniLoaded)
            return;

        var (floorItems, wallItems, ownerNames) = await _roomGrain._itemsLoader.LoadByRoomIdAsync(
            (RoomId)_roomGrain.GetPrimaryKeyLong(),
            ct
        );

        foreach (var (id, name) in ownerNames)
            _state.OwnerNamesById.TryAdd(id, name);

        _state.IsTileComputationPaused = true;

        foreach (var item in floorItems)
            await AddFloorItemAsync(item, ct);

        _state.IsTileComputationPaused = false;

        _roomMap.ComputeAllTiles();
        _state.DirtyHeightTileIds.Clear();

        foreach (var item in wallItems)
            await AddWallItemAsync(item, ct);

        _state.IsFurniLoaded = true;
    }
}
