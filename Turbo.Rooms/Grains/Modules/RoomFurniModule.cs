using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Providers;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomFurniModule(
    RoomGrain roomGrain,
    RoomConfig roomConfig,
    RoomLiveState roomLiveState,
    RoomMapModule roomMapModule,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory,
    IRoomItemsProvider itemsLoader,
    IRoomObjectLogicProvider logicFactory
) : IRoomModule
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomConfig _roomConfig = roomConfig;
    private readonly RoomLiveState _state = roomLiveState;
    private readonly RoomMapModule _roomMap = roomMapModule;
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly IRoomItemsProvider _itemsLoader = itemsLoader;
    private readonly IRoomObjectLogicProvider _logicFactory = logicFactory;

    public Task<ImmutableDictionary<long, string>> GetAllOwnersAsync(CancellationToken ct) =>
        Task.FromResult(_state.OwnerNamesById.ToImmutableDictionary());

    internal async Task EnsureFurniLoadedAsync(CancellationToken ct)
    {
        if (_state.IsFurniLoaded)
            return;

        var (floorItems, wallItems, ownerNames) = await _itemsLoader.LoadByRoomIdAsync(
            _roomGrain.GetPrimaryKeyLong(),
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

    internal async Task FlushDirtyItemIdsAsync(CancellationToken ct)
    {
        await FlushDirtyFloorItemsAsync(ct);
        await FlushDirtyWallItemsAsync(ct);
    }
}
