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
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Chat;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Configuration;

namespace Turbo.Rooms.Grains;

/// <summary>
/// The write buffer of one room, so database writes never hold up the room's turn. Buffered and
/// flushed: the room hands over what changed, a timer writes it, and deactivation writes what
/// is left. A write that fails keeps its rows queued up to the configured caps.
/// </summary>
internal sealed class RoomPersistenceGrain : Grain, IRoomPersistenceGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly RoomConfig _roomConfig;
    private readonly ILogger<IRoomPersistenceGrain> _logger;

    private readonly RoomPersistenceLiveState _state;

    private IDisposable? _dirtyItemsTimer;
    private IDisposable? _chatlogTimer;

    public RoomPersistenceGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<RoomConfig> roomConfig,
        ILogger<IRoomPersistenceGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _roomConfig = roomConfig.Value;
        _logger = logger;

        _state = new() { RoomId = this.GetRoomId() };
    }

    public override Task OnActivateAsync(CancellationToken ct)
    {
        _dirtyItemsTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) => await ((RoomPersistenceGrain)self!).FlushDirtyItemsAsync(ct),
            this,
            TimeSpan.FromMilliseconds(_roomConfig.DirtyItemsTickMs),
            TimeSpan.FromMilliseconds(_roomConfig.DirtyItemsTickMs)
        );

        _chatlogTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) => await ((RoomPersistenceGrain)self!).FlushChatlogsAsync(ct),
            this,
            TimeSpan.FromMilliseconds(_roomConfig.ChatlogTickMs),
            TimeSpan.FromMilliseconds(_roomConfig.ChatlogTickMs)
        );

        return Task.CompletedTask;
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _dirtyItemsTimer?.Dispose();
        _dirtyItemsTimer = null;

        _chatlogTimer?.Dispose();
        _chatlogTimer = null;

        await FlushDirtyItemsAsync(ct);

        // Stops at the first batch that cannot be written: a database that is down must not
        // hold the deactivation in a loop.
        while (_state.PendingChatlogs.Count > 0 && await FlushChatlogsAsync(ct)) { }
    }

    public Task EnqueueChatlogAsync(RoomChatlogSnapshot snapshot, CancellationToken ct)
    {
        if (_state.PendingChatlogs.Count >= _roomConfig.MaxPendingChatlogs)
        {
            _logger.LogWarning(
                "Chatlog queue for room {RoomId} is full ({Max}); dropping oldest entry",
                _state.RoomId,
                _roomConfig.MaxPendingChatlogs
            );

            _state.PendingChatlogs.Dequeue();
        }

        _state.PendingChatlogs.Enqueue(snapshot);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes one batch. False means the batch could not be written and went back on the queue,
    /// which is what tells deactivation to stop trying.
    /// </summary>
    private async Task<bool> FlushChatlogsAsync(CancellationToken ct)
    {
        if (_state.PendingChatlogs.Count == 0)
            return true;

        var batchSize = Math.Min(_state.PendingChatlogs.Count, _roomConfig.MaxChatlogsPerFlush);
        var batch = new List<RoomChatlogEntity>(batchSize);
        var taken = new List<RoomChatlogSnapshot>(batchSize);

        for (var i = 0; i < batchSize; i++)
        {
            var snapshot = _state.PendingChatlogs.Dequeue();

            taken.Add(snapshot);

            var message =
                snapshot.Text.Length > RoomChatlogEntity.MESSAGE_MAX_LENGTH
                    ? snapshot.Text[..RoomChatlogEntity.MESSAGE_MAX_LENGTH]
                    : snapshot.Text;

            batch.Add(
                new RoomChatlogEntity
                {
                    RoomEntityId = snapshot.RoomId.Value,
                    PlayerEntityId = snapshot.PlayerId.Value,
                    TargetPlayerEntityId = snapshot.TargetPlayerId?.Value,
                    Message = message,
                    RoomEntity = null!,
                    PlayerEntity = null!,
                }
            );
        }

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            dbCtx.Chatlogs.AddRange(batch);

            await dbCtx.SaveChangesAsync(ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to flush {Count} chatlog entries for room {RoomId}; they stay queued",
                batch.Count,
                _state.RoomId
            );

            RequeueChatlogs(taken);

            return false;
        }
    }

    /// <summary>Puts a failed batch back in front, oldest first, dropping the newest past the cap.</summary>
    private void RequeueChatlogs(List<RoomChatlogSnapshot> failed)
    {
        var later = _state.PendingChatlogs.ToList();

        _state.PendingChatlogs.Clear();

        foreach (var snapshot in failed.Concat(later).Take(_roomConfig.MaxPendingChatlogs))
            _state.PendingChatlogs.Enqueue(snapshot);
    }

    private async Task FlushDeletedItemsAsync(CancellationToken ct)
    {
        if (_state.DeletedItemIds.Count == 0)
            return;

        var ids = _state.DeletedItemIds.Select(x => x.Value).ToList();

        _state.DeletedItemIds.Clear();

        try
        {
            using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            await dbCtx.Furnitures.Where(x => ids.Contains(x.Id)).ExecuteDeleteAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to delete {Count} furniture items for room {RoomId}",
                ids.Count,
                _state.RoomId
            );

            foreach (var id in ids)
                _state.DeletedItemIds.Add(id);
        }
    }

    public Task EnqueueDirtyItemAsync(
        RoomId roomId,
        RoomItemSnapshot snapshot,
        CancellationToken ct,
        bool remove = false
    )
    {
        _state.DirtyItems[snapshot.ObjectId] = snapshot;

        if (remove)
            _state.RemovedItemIds.Add(snapshot.ObjectId);

        return Task.CompletedTask;
    }

    public Task EnqueueDeletedItemAsync(RoomId roomId, RoomObjectId itemId, CancellationToken ct)
    {
        // A pending update for the same item would only resurrect the row.
        _state.DirtyItems.Remove(itemId);
        _state.RemovedItemIds.Remove(itemId);
        _state.DeletedItemIds.Add(itemId);

        return Task.CompletedTask;
    }

    public Task EnqueueDirtyItemsAsync(
        RoomId roomId,
        List<RoomItemSnapshot> snapshots,
        CancellationToken ct
    )
    {
        foreach (var snapshot in snapshots)
            _state.DirtyItems[snapshot.ObjectId] = snapshot;

        return Task.CompletedTask;
    }

    public Task EnqueueDirtyPetAsync(PetSnapshot snapshot, CancellationToken ct)
    {
        _state.DirtyPets[snapshot.Id] = snapshot;

        return Task.CompletedTask;
    }

    public Task EnqueueDirtyBotAsync(BotSnapshot snapshot, CancellationToken ct)
    {
        _state.DirtyBots[snapshot.Id] = snapshot;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Only a row still standing in this room is written: a pet picked up meanwhile belongs to
    /// the inventory, which wrote it back itself.
    /// </summary>
    private async Task FlushDirtyPetsAsync(CancellationToken ct)
    {
        if (_state.DirtyPets.Count == 0)
            return;

        var batch = _state.DirtyPets.Values.Take(_roomConfig.MaxDirtyItemsPerFlush).ToArray();

        foreach (var pet in batch)
            _state.DirtyPets.Remove(pet.Id);

        var roomId = _state.RoomId.Value;

        try
        {
            using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            foreach (var pet in batch)
            {
                var customParts = PetFigure.SerializeCustomParts(pet.Figure.CustomParts);

                await dbCtx
                    .Pets.Where(x => x.Id == pet.Id && x.RoomEntityId == roomId)
                    .ExecuteUpdateAsync(
                        up =>
                            up.SetProperty(p => p.Name, pet.Name)
                                .SetProperty(p => p.Level, pet.Level)
                                .SetProperty(p => p.Experience, pet.Experience)
                                .SetProperty(p => p.Energy, pet.Energy)
                                .SetProperty(p => p.Nutrition, pet.Nutrition)
                                .SetProperty(p => p.Respect, pet.Respect)
                                .SetProperty(p => p.HasSaddle, pet.HasSaddle)
                                .SetProperty(p => p.AnyoneCanRide, pet.AnyoneCanRide)
                                .SetProperty(
                                    p => p.HasBreedingPermission,
                                    pet.HasBreedingPermission
                                )
                                .SetProperty(p => p.PaletteId, pet.Figure.PaletteId)
                                .SetProperty(p => p.Color, pet.Figure.Color)
                                .SetProperty(p => p.CustomParts, customParts)
                                .SetProperty(p => p.X, pet.X)
                                .SetProperty(p => p.Y, pet.Y)
                                .SetProperty(p => p.Z, pet.Z.Value)
                                .SetProperty(p => p.Rotation, pet.Rotation)
                                .SetProperty(p => p.WateredAt, pet.WateredAtUtc)
                                .SetProperty(p => p.HarvestedAt, pet.HarvestedAtUtc),
                        ct
                    );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to flush {Count} dirty pets for room {RoomId}",
                batch.Length,
                roomId
            );
        }
    }

    private async Task FlushDirtyBotsAsync(CancellationToken ct)
    {
        if (_state.DirtyBots.Count == 0)
            return;

        var batch = _state.DirtyBots.Values.Take(_roomConfig.MaxDirtyItemsPerFlush).ToArray();

        foreach (var bot in batch)
            _state.DirtyBots.Remove(bot.Id);

        var roomId = _state.RoomId.Value;

        try
        {
            using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            foreach (var bot in batch)
            {
                await dbCtx
                    .Bots.Where(x => x.Id == bot.Id && x.RoomEntityId == roomId)
                    .ExecuteUpdateAsync(
                        up =>
                            up.SetProperty(p => p.Name, bot.Name)
                                .SetProperty(p => p.Motto, bot.Motto)
                                .SetProperty(p => p.Figure, bot.Figure)
                                .SetProperty(p => p.Gender, bot.Gender)
                                .SetProperty(p => p.X, bot.X)
                                .SetProperty(p => p.Y, bot.Y)
                                .SetProperty(p => p.Z, bot.Z.Value)
                                .SetProperty(p => p.Rotation, bot.Rotation)
                                .SetProperty(p => p.FreeRoam, bot.FreeRoam)
                                .SetProperty(p => p.ChatText, bot.ChatText)
                                .SetProperty(p => p.AutoChat, bot.AutoChat)
                                .SetProperty(p => p.ChatDelaySeconds, bot.ChatDelaySeconds)
                                .SetProperty(p => p.MixSentences, bot.MixSentences)
                                .SetProperty(p => p.DanceType, bot.DanceType),
                        ct
                    );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to flush {Count} dirty bots for room {RoomId}",
                batch.Length,
                roomId
            );
        }
    }

    private async Task FlushDirtyItemsAsync(CancellationToken ct)
    {
        await FlushDeletedItemsAsync(ct);
        await FlushDirtyPetsAsync(ct);
        await FlushDirtyBotsAsync(ct);

        if (_state.DirtyItems.Count == 0)
            return;

        var batch = _state
            .DirtyItems.Take(_roomConfig.MaxDirtyItemsPerFlush)
            .Select(x => x.Value)
            .ToArray();

        foreach (var item in batch)
            _state.DirtyItems.Remove(item.ObjectId);

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
                    ExtraData = item.ExtraData,
                };

                dbCtx.Attach(dbEntity);

                var e = dbCtx.Entry(dbEntity);

                e.Property(x => x.PlayerEntityId).IsModified = true;
                e.Property(x => x.RoomEntityId).IsModified = true;
                e.Property(x => x.X).IsModified = true;
                e.Property(x => x.Y).IsModified = true;
                e.Property(x => x.Z).IsModified = true;
                e.Property(x => x.Rotation).IsModified = true;
                e.Property(x => x.ExtraData).IsModified = true;

                if (item is RoomWallItemSnapshot wallItem)
                {
                    dbEntity.WallOffset = wallItem.WallOffset;

                    e.Property(x => x.WallOffset).IsModified = true;
                }

                if (_state.RemovedItemIds.Contains(item.ObjectId))
                {
                    dbEntity.RoomEntityId = null;

                    e.Property(x => x.RoomEntityId).IsModified = true;

                    _state.RemovedItemIds.Remove(item.ObjectId);
                }
                else
                {
                    dbEntity.RoomEntityId = _state.RoomId.Value;

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
                _state.RoomId
            );
        }
    }
}
