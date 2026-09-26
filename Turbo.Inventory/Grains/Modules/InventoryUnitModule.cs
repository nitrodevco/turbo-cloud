using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Rooms;

namespace Turbo.Inventory.Grains.Modules;

/// <summary>
/// The pets or the bots a player keeps in the inventory (not standing in a room). The two were
/// one flow written twice; this is that flow once, and a subclass says only what differs: the
/// table, the snapshot, the owned limit, what a room hands back, and how the client is told.
///
/// Loaded on first use. Every hand-over to or from a room writes the row before the list
/// changes, so a crash in between leaves the unit where the database says it is.
/// </summary>
internal abstract class InventoryUnitModule<TEntity, TSnapshot>(
    InventoryGrain inventoryGrain,
    InventoryUnitSection<TSnapshot> section,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger logger
)
    where TEntity : TurboEntity, IInventoryUnitEntity
    where TSnapshot : class, IInventoryUnitSnapshot
{
    protected readonly InventoryGrain _inventoryGrain = inventoryGrain;
    protected readonly InventoryUnitSection<TSnapshot> _section = section;
    protected readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    protected readonly ILogger _logger = logger;

    protected int OwnerId => (int)_inventoryGrain.PlayerId;

    /// <summary>"pet" or "bot", for the logs.</summary>
    protected abstract string Kind { get; }

    /// <summary>How many a player may own, counting the ones standing in rooms.</summary>
    protected abstract int MaxOwned { get; }

    protected abstract DbSet<TEntity> Table(TurboDbContext dbCtx);

    protected abstract TSnapshot ToSnapshot(TEntity entity, string ownerName);

    protected abstract TSnapshot WithRoom(TSnapshot snapshot, RoomId? roomId);

    /// <summary>
    /// Writes what the unit brings back from a room — a pet's stats, a bot's settings — and
    /// clears its room. <paramref name="row"/> is already narrowed to the owner's row.
    /// </summary>
    protected abstract Task<int> WriteReturnedAsync(
        IQueryable<TEntity> row,
        TSnapshot snapshot,
        CancellationToken ct
    );

    protected abstract Task OnAddedAsync(
        TSnapshot snapshot,
        bool openInventory,
        CancellationToken ct
    );

    protected abstract Task OnRemovedAsync(int id, CancellationToken ct);

    public async Task EnsureReadyAsync(CancellationToken ct)
    {
        if (_section.IsReady)
            return;

        var ownerName = await _inventoryGrain.GetOwnerNameAsync(ct);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entities = await Table(dbCtx)
            .AsNoTracking()
            .Where(x => x.PlayerEntityId == OwnerId && x.RoomEntityId == null)
            .ToListAsync(ct);

        _section.ById.Clear();

        foreach (var entity in entities)
            _section.ById[entity.Id] = ToSnapshot(entity, ownerName);

        _section.IsReady = true;
    }

    public async Task<ImmutableArray<TSnapshot>> GetAllAsync(CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        return [.. _section.ById.Values];
    }

    public async Task<TSnapshot?> GetAsync(int id, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        return _section.ById.TryGetValue(id, out var unit) ? unit : null;
    }

    /// <summary>Hands a unit to a room. Null when it is not here to give.</summary>
    public async Task<TSnapshot?> TryCheckOutAsync(int id, RoomId roomId, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        if (!_section.ById.TryGetValue(id, out var unit))
            return null;

        int updated;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            updated = await Table(dbCtx)
                .Where(x => x.Id == id && x.PlayerEntityId == OwnerId && x.RoomEntityId == null)
                .ExecuteUpdateAsync(up => up.SetProperty(x => x.RoomEntityId, roomId.Value), ct);
        }

        if (updated == 0)
        {
            _logger.LogWarning(
                "{Kind} {UnitId} of player {PlayerId} is listed in the inventory but its row is elsewhere; reloading",
                Kind,
                id,
                _inventoryGrain.PlayerId
            );

            _section.IsReady = false;

            return null;
        }

        _section.ById.Remove(id);

        await OnRemovedAsync(id, ct);

        return WithRoom(unit, roomId);
    }

    /// <summary>Takes a unit back from a room, with what it brought back.</summary>
    public async Task<bool> ReturnAsync(TSnapshot snapshot, CancellationToken ct)
    {
        if (snapshot.OwnerId != _inventoryGrain.PlayerId)
            return false;

        await EnsureReadyAsync(ct);

        int updated;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            updated = await WriteReturnedAsync(
                Table(dbCtx).Where(x => x.Id == snapshot.Id && x.PlayerEntityId == OwnerId),
                snapshot,
                ct
            );
        }

        if (updated == 0)
        {
            _logger.LogError(
                "{Kind} {UnitId} returned to player {PlayerId} has no row to update",
                Kind,
                snapshot.Id,
                _inventoryGrain.PlayerId
            );

            return false;
        }

        var returned = WithRoom(snapshot, null);

        _section.ById[returned.Id] = returned;

        await OnAddedAsync(returned, false, ct);

        return true;
    }

    /// <summary>
    /// Adds a new unit to the inventory. Null when the player already owns
    /// <see cref="MaxOwned"/>, counting the ones standing in rooms.
    /// </summary>
    protected async Task<TSnapshot?> CreateAsync(TEntity entity, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            var owned = await Table(dbCtx).CountAsync(x => x.PlayerEntityId == OwnerId, ct);

            if (owned >= MaxOwned)
            {
                _logger.LogWarning(
                    "Player {PlayerId} owns {Count} of kind {Kind}, the configured maximum; not creating another",
                    _inventoryGrain.PlayerId,
                    owned,
                    Kind
                );

                return null;
            }

            dbCtx.Add(entity);

            await dbCtx.SaveChangesAsync(ct);
        }

        var snapshot = ToSnapshot(entity, await _inventoryGrain.GetOwnerNameAsync(ct));

        _section.ById[snapshot.Id] = snapshot;

        await OnAddedAsync(snapshot, true, ct);

        return snapshot;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        int deleted;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            deleted = await Table(dbCtx)
                .Where(x => x.Id == id && x.PlayerEntityId == OwnerId)
                .ExecuteDeleteAsync(ct);
        }

        if (deleted == 0)
            return false;

        if (_section.ById.Remove(id))
            await OnRemovedAsync(id, ct);

        return true;
    }
}
