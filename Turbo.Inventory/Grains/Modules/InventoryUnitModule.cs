using System;
using System.Collections.Immutable;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities;
using Turbo.Database.Entities.Pets;
using Turbo.Database.Extensions;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets.Snapshots;
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
) : InventoryGrainComponent(inventoryGrain)
    where TEntity : TurboEntity, IInventoryUnitEntity
    where TSnapshot : class, IInventoryUnitSnapshot
{
    protected readonly InventoryUnitSection<TSnapshot> _section = section;
    protected readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    protected readonly ILogger _logger = logger;

    /// <summary>"pet" or "bot", for the logs.</summary>
    protected abstract string Kind { get; }

    /// <summary>The inventory tab a new one is marked "new" in.</summary>
    protected abstract UnseenItemCategory UnseenCategory { get; }

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

        var ownerName = await GetOwnerNameAsync(ct);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entities = await Table(dbCtx)
            .AsNoTracking()
            .Where(x => x.PlayerEntityId == PlayerId.Value && x.RoomEntityId == null)
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
                .Where(x =>
                    x.Id == id && x.PlayerEntityId == PlayerId.Value && x.RoomEntityId == null
                )
                .ExecuteUpdateAsync(up => up.SetProperty(x => x.RoomEntityId, roomId.Value), ct);
        }

        if (updated == 0)
        {
            _logger.LogWarning(
                "{Kind} {UnitId} of player {PlayerId} is listed in the inventory but its row is elsewhere; reloading",
                Kind,
                id,
                PlayerId
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
        if (snapshot.OwnerId != PlayerId)
            return false;

        await EnsureReadyAsync(ct);

        int updated;

        var originalSnapshot = snapshot;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                snapshot = originalSnapshot;
                await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);
                await using var transaction = await dbCtx.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    ct
                );
                var previousPet =
                    snapshot is PetSnapshot
                        ? await dbCtx
                            .Pets.Where(x =>
                                x.Id == snapshot.Id && x.PlayerEntityId == PlayerId.Value
                            )
                            .Select(x => new
                            {
                                x.Level,
                                x.Experience,
                                x.Respect,
                            })
                            .SingleOrDefaultAsync(ct)
                        : null;
                if (snapshot is PetSnapshot pet && previousPet is not null)
                    snapshot = (TSnapshot)
                        (object)(
                            pet with
                            {
                                Level = Math.Max(pet.Level, previousPet.Level),
                                Experience = Math.Max(pet.Experience, previousPet.Experience),
                                Respect = Math.Max(pet.Respect, previousPet.Respect),
                            }
                        );
                updated = await WriteReturnedAsync(
                    Table(dbCtx)
                        .Where(x => x.Id == snapshot.Id && x.PlayerEntityId == PlayerId.Value),
                    snapshot,
                    ct
                );
                if (
                    updated > 0
                    && snapshot is PetSnapshot returnedPet
                    && previousPet is not null
                    && returnedPet.Level > previousPet.Level
                )
                    _inventoryGrain._achievementFacts.Record(
                        dbCtx,
                        PlayerId,
                        new()
                        {
                            Source = AchievementSources.PET_LEVEL,
                            OperationId = Guid.NewGuid().ToString("N"),
                            Amount = returnedPet.Level - previousPet.Level,
                            OccurredAtUtc = DateTime.UtcNow,
                        }
                    );
                await dbCtx.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                break;
            }
            catch (Exception ex) when (attempt < 2 && ex.IsRetryableWriteConflict())
            {
                await Task.Delay(TimeSpan.FromMilliseconds(25 * (attempt + 1)), ct);
            }
        }

        if (updated == 0)
        {
            _logger.LogError(
                "{Kind} {UnitId} returned to player {PlayerId} has no row to update",
                Kind,
                snapshot.Id,
                PlayerId
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
            var owned = await Table(dbCtx).CountAsync(x => x.PlayerEntityId == PlayerId.Value, ct);

            if (owned >= MaxOwned)
            {
                _logger.LogWarning(
                    "Player {PlayerId} owns {Count} of kind {Kind}, the configured maximum; not creating another",
                    PlayerId,
                    owned,
                    Kind
                );

                return null;
            }

            dbCtx.Add(entity);
            if (entity is PetEntity)
                _inventoryGrain._achievementFacts.Record(
                    dbCtx,
                    PlayerId,
                    new()
                    {
                        Source = AchievementSources.PETS,
                        OperationId = Guid.NewGuid().ToString("N"),
                        Amount = owned + 1,
                        OccurredAtUtc = DateTime.UtcNow,
                    }
                );

            await dbCtx.SaveChangesAsync(ct);
        }

        var snapshot = ToSnapshot(entity, await GetOwnerNameAsync(ct));

        _section.ById[snapshot.Id] = snapshot;

        await OnAddedAsync(snapshot, true, ct);

        // New until the player opens the tab; one coming back from a room is not.
        UnseenItems
            .AddAsync(UnseenCategory, [snapshot.Id], CancellationToken.None)
            .LogAndForget(_logger, "mark a new {Kind} for player {PlayerId}", Kind, PlayerId);

        return snapshot;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        await EnsureReadyAsync(ct);

        int deleted;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            // Keep the recipient row available until a durably admitted respect finishes.
            if (
                typeof(TEntity) == typeof(PetEntity)
                && await dbCtx.PetRespectOperations.AnyAsync(
                    x => x.PetId == id && !x.Completed && !x.Rejected,
                    ct
                )
            )
                return false;
            deleted = await Table(dbCtx)
                .Where(x => x.Id == id && x.PlayerEntityId == PlayerId.Value)
                .ExecuteDeleteAsync(ct);
        }

        if (deleted == 0)
            return false;

        if (_section.ById.Remove(id))
            await OnRemovedAsync(id, ct);

        return true;
    }
}
