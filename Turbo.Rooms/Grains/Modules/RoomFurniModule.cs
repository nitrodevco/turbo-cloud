using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Factories;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomFurniModule(
    RoomGrain roomGrain,
    RoomConfig roomConfig,
    RoomLiveState roomLiveState,
    RoomMapModule roomMapModule,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IRoomItemsLoader itemsLoader,
    IRoomObjectLogicFactory logicFactory
) : IRoomModule
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomConfig _roomConfig = roomConfig;
    private readonly RoomLiveState _state = roomLiveState;
    private readonly RoomMapModule _roomMap = roomMapModule;
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly IRoomItemsLoader _itemsLoader = itemsLoader;
    private readonly IRoomObjectLogicFactory _logicFactory = logicFactory;

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

        foreach (var item in floorItems)
            await AddFloorItemAsync(item, ct);

        _state.IsFurniLoaded = true;
    }

    internal async Task FlushDirtyItemIdsAsync(CancellationToken ct)
    {
        if (_state.DirtyItemIds.Count == 0)
            return;

        var dirtyItemIds = _state.DirtyItemIds.ToArray();

        _state.DirtyItemIds.Clear();

        var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        try
        {
            var dirtySnapshots = dirtyItemIds
                .Select(id => _state.FloorItemsById[id].GetSnapshot())
                .ToArray();
            var dirtyIds = dirtySnapshots.Select(x => x.ObjectId.Value).ToArray();
            var entities = await dbCtx
                .Furnitures.Where(x => dirtyIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);

            foreach (var snapshot in dirtySnapshots)
            {
                if (!entities.TryGetValue(snapshot.ObjectId.Value, out var entity))
                    continue;

                entity.X = snapshot.X;
                entity.Y = snapshot.Y;
                entity.Z = snapshot.Z;
                entity.Rotation = snapshot.Rotation;
                entity.StuffData = snapshot.StuffDataJson;
            }

            await dbCtx.SaveChangesAsync(ct);
        }
        catch (Exception)
        {
            throw;
        }
        finally
        {
            await dbCtx.DisposeAsync();
        }
    }
}
