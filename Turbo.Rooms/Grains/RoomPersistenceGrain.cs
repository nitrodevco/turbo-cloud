using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Furniture;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Grains;

public sealed class RoomPersistenceGrain(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<RoomConfig> roomConfig,
    ILogger<IRoomPersistenceGrain> logger
) : Grain, IRoomPersistenceGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly RoomConfig _roomConfig = roomConfig.Value;
    private readonly ILogger<IRoomPersistenceGrain> _logger = logger;

    private Dictionary<long, RoomItemSnapshot> _dirtyItems = [];
    private readonly HashSet<RoomObjectId> _removedItemIds = [];
    private IDisposable? _timer;

    public override Task OnActivateAsync(CancellationToken ct)
    {
        _timer = this.RegisterGrainTimer<object?>(
            static async (self, ct) => await ((RoomPersistenceGrain)self!).FlushDirtyItemsAsync(ct),
            this,
            TimeSpan.FromMilliseconds(_roomConfig.DirtyItemsFlushIntervalMilliseconds),
            TimeSpan.FromMilliseconds(_roomConfig.DirtyItemsFlushIntervalMilliseconds)
        );

        return Task.CompletedTask;
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        await FlushDirtyItemsAsync(ct);
    }

    public Task EnqueueDirtyItemAsync(
        RoomId roomId,
        RoomItemSnapshot snapshot,
        CancellationToken ct,
        bool remove = false
    )
    {
        _dirtyItems[snapshot.ObjectId] = snapshot;

        if (remove)
            _removedItemIds.Add(snapshot.ObjectId);

        return Task.CompletedTask;
    }

    public Task EnqueueDirtyItemsAsync(
        RoomId roomId,
        List<RoomItemSnapshot> snapshots,
        CancellationToken ct
    )
    {
        foreach (var snapshot in snapshots)
            _dirtyItems[snapshot.ObjectId] = snapshot;

        return Task.CompletedTask;
    }

    private async Task FlushDirtyItemsAsync(CancellationToken ct)
    {
        if (_dirtyItems.Count == 0)
            return;

        var batch = _dirtyItems
            .Take(_roomConfig.MaxDirtyItemsPerFlush)
            .Select(x => x.Value)
            .ToArray();

        foreach (var item in batch)
            _dirtyItems.Remove(item.ObjectId);

        try
        {
            using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            foreach (var item in batch)
            {
                var dbEntity = new FurnitureEntity
                {
                    Id = item.ObjectId.Value,
                    PlayerEntityId = item.OwnerId.Value,
                    X = item.X,
                    Y = item.Y,
                    Z = item.Z,
                    Rotation = item.Rotation,
                    StuffData = item.StuffDataJson,
                };

                dbCtx.Attach(dbEntity);

                var e = dbCtx.Entry(dbEntity);

                e.Property(x => x.PlayerEntityId).IsModified = true;
                e.Property(x => x.RoomEntityId).IsModified = true;
                e.Property(x => x.X).IsModified = true;
                e.Property(x => x.Y).IsModified = true;
                e.Property(x => x.Z).IsModified = true;
                e.Property(x => x.Rotation).IsModified = true;
                e.Property(x => x.StuffData).IsModified = true;

                if (item is RoomWallItemSnapshot wallItem)
                {
                    dbEntity.WallOffset = wallItem.WallOffset;

                    e.Property(x => x.WallOffset).IsModified = true;
                }

                if (_removedItemIds.Contains(item.ObjectId))
                {
                    dbEntity.RoomEntityId = null;

                    e.Property(x => x.RoomEntityId).IsModified = true;

                    _removedItemIds.Remove(item.ObjectId);
                }
                else
                {
                    dbEntity.RoomEntityId = (int)this.GetPrimaryKeyLong();

                    e.Property(x => x.RoomEntityId).IsModified = true;
                }
            }

            await dbCtx.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to flush {Count} dirty furniture items for room {RoomId}",
                batch.Length,
                this.GetPrimaryKeyLong()
            );
        }
    }
}
