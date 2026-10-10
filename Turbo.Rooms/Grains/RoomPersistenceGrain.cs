using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Achievements;
using Turbo.Database.Context;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Pets;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Bots.Snapshots;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Commands.Snapshots;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Pets.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
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
/// is left. A write that fails keeps its rows queued for the next tick. Pet care
/// operations are the write-through exception: their pet mutation and achievement receipt commit
/// together, then their result is merged into any buffered pet snapshot before it is returned.
/// </summary>
internal sealed class RoomPersistenceGrain : Grain, IRoomPersistenceGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly RoomConfig _roomConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IRoomPersistenceGrain> _logger;
    private readonly IAchievementFactRecorder _achievementFacts;

    private readonly RoomPersistenceLiveState _state;

    private IDisposable? _dirtyItemsTimer;
    private IDisposable? _chatlogTimer;

    public RoomPersistenceGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<RoomConfig> roomConfig,
        IGrainFactory grainFactory,
        IAchievementFactRecorder achievementFacts,
        ILogger<IRoomPersistenceGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _roomConfig = roomConfig.Value;
        _grainFactory = grainFactory;
        _logger = logger;
        _achievementFacts = achievementFacts;

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
            static async (self, ct) => await ((RoomPersistenceGrain)self!).FlushLogsAsync(ct),
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

        // Everything that is left, not one timer's worth. Each queue still stops at its first
        // batch that cannot be written: a database that is down must not hold the deactivation
        // in a loop.
        await FlushDirtyItemsAsync(int.MaxValue, ct);

        while (_state.PendingChatlogs.Count > 0 && await FlushChatlogsAsync(ct)) { }

        while (_state.PendingCommandLogs.Count > 0 && await FlushCommandLogsAsync(ct)) { }
    }

    /// <summary>The chatlog timer's tick: the two audit buffers share it, and neither holds up the other.</summary>
    private async Task FlushLogsAsync(CancellationToken ct)
    {
        await FlushChatlogsAsync(ct);
        await FlushCommandLogsAsync(ct);
    }

    public Task EnqueueCommandLogsAsync(List<CommandLogSnapshot> snapshots, CancellationToken ct)
    {
        var dropped = 0;

        foreach (var snapshot in snapshots)
        {
            if (_state.PendingCommandLogs.Count >= _roomConfig.MaxPendingCommandLogs)
            {
                _state.PendingCommandLogs.Dequeue();
                dropped++;
            }

            _state.PendingCommandLogs.Enqueue(snapshot);
        }

        if (dropped > 0)
            _logger.LogWarning(
                "Command log queue for room {RoomId} is full ({Max}); dropped the {Count} oldest entries",
                _state.RoomId,
                _roomConfig.MaxPendingCommandLogs,
                dropped
            );

        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes one batch of command uses. False means it could not be written and went back on the
    /// queue, which is what tells deactivation to stop trying.
    /// </summary>
    private async Task<bool> FlushCommandLogsAsync(CancellationToken ct)
    {
        if (_state.PendingCommandLogs.Count == 0)
            return true;

        var batchSize = Math.Min(
            _state.PendingCommandLogs.Count,
            _roomConfig.MaxCommandLogsPerFlush
        );
        var taken = new List<CommandLogSnapshot>(batchSize);

        for (var i = 0; i < batchSize; i++)
            taken.Add(_state.PendingCommandLogs.Dequeue());

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            dbCtx.CommandLogs.AddRange(
                taken.Select(x => new CommandLogEntity
                {
                    RoomEntityId = x.RoomId.Value,
                    PlayerEntityId = x.PlayerId.Value,
                    Command = Truncate(x.Command, CommandLogEntity.COMMAND_MAX_LENGTH),
                    Arguments = Truncate(x.Arguments, CommandLogEntity.ARGUMENTS_MAX_LENGTH),
                    Outcome = Truncate(
                        CommandTelemetry.Name(x.Outcome),
                        CommandLogEntity.OUTCOME_MAX_LENGTH
                    ),
                    CreatedAt = x.LoggedAtUtc,
                })
            );

            await dbCtx.SaveChangesAsync(ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to flush {Count} command log entries for room {RoomId}; they stay queued",
                taken.Count,
                _state.RoomId
            );

            var later = _state.PendingCommandLogs.ToList();

            _state.PendingCommandLogs.Clear();

            foreach (var snapshot in taken.Concat(later).Take(_roomConfig.MaxPendingCommandLogs))
                _state.PendingCommandLogs.Enqueue(snapshot);

            return false;
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length > max ? value[..max] : value;

    public Task EnqueueChatlogsAsync(List<RoomChatlogSnapshot> snapshots, CancellationToken ct)
    {
        var dropped = 0;

        foreach (var snapshot in snapshots)
        {
            if (_state.PendingChatlogs.Count >= _roomConfig.MaxPendingChatlogs)
            {
                _state.PendingChatlogs.Dequeue();
                dropped++;
            }

            _state.PendingChatlogs.Enqueue(snapshot);
        }

        if (dropped > 0)
            _logger.LogWarning(
                "Chatlog queue for room {RoomId} is full ({Max}); dropped the {Count} oldest entries",
                _state.RoomId,
                _roomConfig.MaxPendingChatlogs,
                dropped
            );

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

        // Borrowed furni lives in its own table, keyed by this room and the id the room gave it.
        var borrowedIds = ids.Where(FurniIdBands.IsBuildersClub).ToList();
        var ownedIds = ids.Where(x => !FurniIdBands.IsBuildersClub(x)).ToList();

        try
        {
            using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            if (ownedIds.Count > 0)
                await dbCtx.Furnitures.Where(x => ownedIds.Contains(x.Id)).ExecuteDeleteAsync(ct);

            if (borrowedIds.Count > 0)
                await dbCtx
                    .BuildersClubFurnitures.Where(x =>
                        x.RoomEntityId == _state.RoomId.Value
                        && borrowedIds.Contains(x.RoomObjectId)
                    )
                    .ExecuteDeleteAsync(ct);
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
        {
            _state.DirtyItems[snapshot.ObjectId] = snapshot;

            // The room hands over only what stands in it. An item picked up and put back before
            // the next flush still had its pick-up queued, which wrote it out of the room again.
            _state.RemovedItemIds.Remove(snapshot.ObjectId);
        }

        return Task.CompletedTask;
    }

    public Task EnqueueDirtyPetsAsync(List<PetSnapshot> snapshots, CancellationToken ct)
    {
        foreach (var snapshot in snapshots)
            _state.DirtyPets[snapshot.Id] = snapshot;

        return Task.CompletedTask;
    }

    public async Task<PetNutritionOperationResult> ApplyPetNutritionOperationAsync(
        string operationId,
        int petId,
        PlayerId ownerId,
        PlayerId supplierId,
        int baseNutrition,
        int requestedNutrition,
        int maxNutrition,
        CancellationToken ct
    )
    {
        ValidateOperationId(operationId);
        if (
            petId <= 0
            || ownerId.Value <= 0
            || baseNutrition < 0
            || requestedNutrition < 0
            || maxNutrition < 0
        )
            throw new ArgumentOutOfRangeException(
                nameof(petId),
                "Invalid pet nutrition operation."
            );

        var roomId = _state.RoomId.Value;
        await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);
        var previous = await db.PetNutritionOperations.SingleOrDefaultAsync(
            x => x.OperationId == operationId,
            ct
        );
        if (previous is not null)
        {
            EnsureSameNutritionOperation(
                previous,
                roomId,
                petId,
                ownerId,
                supplierId,
                baseNutrition,
                requestedNutrition,
                maxNutrition
            );
            if (!previous.Completed)
                throw new InvalidOperationException("Pet nutrition operation is not complete.");
            MergePetNutrition(
                previous.PetId,
                previous.BaseNutrition,
                previous.NutritionAfter,
                replay: true
            );
            return new() { Nutrition = previous.NutritionAfter, ActualGain = previous.ActualGain };
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var pet = await db.Pets.SingleOrDefaultAsync(
            x => x.Id == petId && x.PlayerEntityId == ownerId.Value,
            ct
        );
        if (pet is null)
            throw new InvalidOperationException(
                "Pet nutrition operation refers to a missing or transferred pet."
            );

        var actualGain = Math.Min(requestedNutrition, Math.Max(0, maxNutrition - baseNutrition));
        var nutritionAfter = checked(baseNutrition + actualGain);
        pet.Nutrition = nutritionAfter;
        var operation = new PetNutritionOperationEntity
        {
            OperationId = operationId,
            RoomId = roomId,
            PetId = petId,
            OwnerId = ownerId.Value,
            SupplierId = supplierId.Value,
            BaseNutrition = baseNutrition,
            RequestedNutrition = requestedNutrition,
            MaxNutrition = maxNutrition,
            NutritionAfter = nutritionAfter,
            ActualGain = actualGain,
            Completed = true,
        };
        db.PetNutritionOperations.Add(operation);
        if (actualGain > 0 && supplierId.Value > 0)
            _achievementFacts.Record(
                db,
                supplierId,
                new()
                {
                    OperationId = $"pet-nutrition:{operationId}:supplied",
                    Source = AchievementSources.NUTRITION,
                    OccurredAtUtc = DateTime.UtcNow,
                    Amount = actualGain,
                }
            );

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        MergePetNutrition(petId, baseNutrition, nutritionAfter, replay: false);
        return new() { Nutrition = nutritionAfter, ActualGain = actualGain };
    }

    public async Task<PetRespectOperationResult> ApplyPetRespectOperationAsync(
        string operationId,
        PlayerId actorId,
        int petId,
        PlayerId ownerId,
        int baseRespect,
        CancellationToken ct
    )
    {
        ValidateOperationId(operationId);
        if (actorId.Value <= 0 || petId <= 0 || ownerId.Value <= 0 || baseRespect < 0)
            throw new ArgumentOutOfRangeException(nameof(petId), "Invalid pet respect operation.");

        var roomId = _state.RoomId.Value;
        await using (var admissionDb = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            var operation = await admissionDb.PetRespectOperations.SingleOrDefaultAsync(
                x => x.OperationId == operationId,
                ct
            );
            if (operation is null)
            {
                operation = new PetRespectOperationEntity
                {
                    OperationId = operationId,
                    RoomId = roomId,
                    ActorId = actorId.Value,
                    PetId = petId,
                    OwnerId = ownerId.Value,
                    BaseRespect = baseRespect,
                };
                admissionDb.PetRespectOperations.Add(operation);
                await admissionDb.SaveChangesAsync(ct);
            }
            else
            {
                EnsureSamePetRespectOperation(
                    operation,
                    roomId,
                    actorId,
                    petId,
                    ownerId,
                    baseRespect
                );
                if (operation.Rejected)
                    return new() { Accepted = false };
                if (operation.Completed)
                {
                    MergePetRespect(petId, operation.ResultRespect);
                    return new() { Accepted = true, Respect = operation.ResultRespect };
                }
            }
        }

        var spent = await _grainFactory
            .GetPlayerGrain(actorId)
            .SpendPetRespectOperationAsync(operationId, ct);
        if (!spent)
        {
            await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);
            var operation = await GetPetRespectOperationAsync(
                db,
                operationId,
                roomId,
                actorId,
                petId,
                ownerId,
                baseRespect,
                ct
            );
            operation.Rejected = true;
            await db.SaveChangesAsync(ct);
            _logger.LogWarning(
                "Pet respect operation {OperationId} from player {ActorId} to pet {PetId} was rejected because no pet respect remained",
                operationId,
                actorId,
                petId
            );
            return new() { Accepted = false };
        }

        await using (var db = await _dbCtxFactory.CreateDbContextAsync(ct))
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            var operation = await GetPetRespectOperationAsync(
                db,
                operationId,
                roomId,
                actorId,
                petId,
                ownerId,
                baseRespect,
                ct
            );
            if (operation.Rejected)
                throw new InvalidOperationException(
                    "A spent pet respect operation is marked rejected."
                );
            if (operation.Completed)
            {
                MergePetRespect(petId, operation.ResultRespect);
                return new() { Accepted = true, Respect = operation.ResultRespect };
            }

            var pet = await db.Pets.SingleOrDefaultAsync(
                x => x.Id == petId && x.PlayerEntityId == ownerId.Value,
                ct
            );
            if (pet is null)
                throw new InvalidOperationException(
                    "Pet respect operation refers to a missing or transferred pet."
                );

            var resultRespect = checked(Math.Max(baseRespect, pet.Respect) + 1);
            pet.Respect = resultRespect;
            var occurredAt = DateTime.UtcNow;
            _achievementFacts.Record(
                db,
                actorId,
                new()
                {
                    OperationId = $"pet-respect:{operationId}:given",
                    Source = AchievementSources.PET_RESPECT_GIVEN,
                    OccurredAtUtc = occurredAt,
                }
            );
            _achievementFacts.Record(
                db,
                ownerId,
                new()
                {
                    OperationId = $"pet-respect:{operationId}:received",
                    Source = AchievementSources.PET_RESPECT_RECEIVED,
                    OccurredAtUtc = occurredAt,
                }
            );
            operation.Completed = true;
            operation.ResultRespect = resultRespect;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            MergePetRespect(petId, resultRespect);
            return new() { Accepted = true, Respect = resultRespect };
        }
    }

    private static void ValidateOperationId(string operationId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (operationId.Length > 100)
            throw new ArgumentException("Pet operation id is too long.", nameof(operationId));
    }

    private static void EnsureSameNutritionOperation(
        PetNutritionOperationEntity operation,
        int roomId,
        int petId,
        PlayerId ownerId,
        PlayerId supplierId,
        int baseNutrition,
        int requestedNutrition,
        int maxNutrition
    )
    {
        if (
            operation.RoomId != roomId
            || operation.PetId != petId
            || operation.OwnerId != ownerId.Value
            || operation.SupplierId != supplierId.Value
            || operation.BaseNutrition != baseNutrition
            || operation.RequestedNutrition != requestedNutrition
            || operation.MaxNutrition != maxNutrition
        )
            throw new InvalidOperationException(
                "Pet nutrition operation id is bound to different input."
            );
    }

    private static void EnsureSamePetRespectOperation(
        PetRespectOperationEntity operation,
        int roomId,
        PlayerId actorId,
        int petId,
        PlayerId ownerId,
        int baseRespect
    )
    {
        if (
            operation.RoomId != roomId
            || operation.ActorId != actorId.Value
            || operation.PetId != petId
            || operation.OwnerId != ownerId.Value
            || operation.BaseRespect != baseRespect
        )
            throw new InvalidOperationException(
                "Pet respect operation id is bound to different input."
            );
    }

    private static async Task<PetRespectOperationEntity> GetPetRespectOperationAsync(
        TurboDbContext db,
        string operationId,
        int roomId,
        PlayerId actorId,
        int petId,
        PlayerId ownerId,
        int baseRespect,
        CancellationToken ct
    )
    {
        var operation = await db.PetRespectOperations.SingleAsync(
            x => x.OperationId == operationId,
            ct
        );
        EnsureSamePetRespectOperation(operation, roomId, actorId, petId, ownerId, baseRespect);
        return operation;
    }

    private void MergePetNutrition(int petId, int baseNutrition, int nutritionAfter, bool replay)
    {
        if (
            _state.DirtyPets.TryGetValue(petId, out var snapshot)
            && (!replay || snapshot.Nutrition == baseNutrition)
        )
            _state.DirtyPets[petId] = snapshot with { Nutrition = nutritionAfter };
    }

    private void MergePetRespect(int petId, int respectAfter)
    {
        if (!_state.DirtyPets.TryGetValue(petId, out var snapshot))
            return;

        // A retry may finish after another committed or buffered respect. Never replace that
        // newer value with the result captured by this operation.
        _state.DirtyPets[petId] = snapshot with
        {
            Respect = Math.Max(snapshot.Respect, respectAfter),
        };
    }

    public Task EnqueueDirtyBotsAsync(List<BotSnapshot> snapshots, CancellationToken ct)
    {
        foreach (var snapshot in snapshots)
            _state.DirtyBots[snapshot.Id] = snapshot;

        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes one batch of pets. Only a row still standing in this room is written: a pet
    /// picked up meanwhile belongs to the inventory, which wrote it back itself. The rows are
    /// read in one query and saved in one, rather than one update statement per pet; the read
    /// is what keeps the "still in this room" check that a keyed update would lose. False when
    /// the batch could not be written.
    /// </summary>
    private async Task<bool> FlushDirtyPetBatchAsync(CancellationToken ct)
    {
        var batch = _state.DirtyPets.Values.Take(_roomConfig.MaxDirtyItemsPerFlush).ToList();

        foreach (var pet in batch)
            _state.DirtyPets.Remove(pet.Id);

        var roomId = _state.RoomId.Value;

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var ids = batch.Select(x => x.Id).ToList();
            await using var transaction = await dbCtx.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                ct
            );
            var rows = await dbCtx
                .Pets.Where(x => ids.Contains(x.Id) && x.RoomEntityId == roomId)
                .ToDictionaryAsync(x => x.Id, ct);

            foreach (var pet in batch)
            {
                if (!rows.TryGetValue(pet.Id, out var row))
                    continue;

                row.Name = pet.Name;
                if (pet.Level > row.Level)
                    _achievementFacts.Record(
                        dbCtx,
                        pet.OwnerId,
                        new()
                        {
                            Source = AchievementSources.PET_LEVEL,
                            Amount = pet.Level - row.Level,
                            OperationId = Guid.NewGuid().ToString("N"),
                            OccurredAtUtc = DateTime.UtcNow,
                        }
                    );
                row.Level = Math.Max(row.Level, pet.Level);
                row.Experience = Math.Max(row.Experience, pet.Experience);
                row.Energy = pet.Energy;
                row.Nutrition = pet.Nutrition;
                row.Respect = Math.Max(row.Respect, pet.Respect);
                row.HasSaddle = pet.HasSaddle;
                row.AnyoneCanRide = pet.AnyoneCanRide;
                row.HasBreedingPermission = pet.HasBreedingPermission;
                row.PaletteId = pet.Figure.PaletteId;
                row.Color = pet.Figure.Color;
                row.CustomParts = PetFigure.SerializeCustomParts(pet.Figure.CustomParts);
                row.X = pet.X;
                row.Y = pet.Y;
                row.Z = pet.Z.Value;
                row.Rotation = pet.Rotation;
                row.WateredAt = pet.WateredAtUtc;
                row.HarvestedAt = pet.HarvestedAtUtc;
                row.EnergyDecayDueAt = pet.EnergyDecayDueUtc;
                row.NutritionDecayDueAt = pet.NutritionDecayDueUtc;
            }

            await dbCtx.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to flush {Count} dirty pets for room {RoomId}",
                batch.Count,
                roomId
            );
            foreach (var pet in batch)
                _state.DirtyPets.TryAdd(pet.Id, pet);

            return false;
        }
    }

    /// <summary>Writes one batch of bots, as <see cref="FlushDirtyPetBatchAsync"/> writes pets.</summary>
    private async Task<bool> FlushDirtyBotBatchAsync(CancellationToken ct)
    {
        var batch = _state.DirtyBots.Values.Take(_roomConfig.MaxDirtyItemsPerFlush).ToList();

        foreach (var bot in batch)
            _state.DirtyBots.Remove(bot.Id);

        var roomId = _state.RoomId.Value;

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var ids = batch.Select(x => x.Id).ToList();
            var rows = await dbCtx
                .Bots.Where(x => ids.Contains(x.Id) && x.RoomEntityId == roomId)
                .ToDictionaryAsync(x => x.Id, ct);

            foreach (var bot in batch)
            {
                if (!rows.TryGetValue(bot.Id, out var row))
                    continue;

                row.Name = bot.Name;
                row.Motto = bot.Motto;
                row.Figure = bot.Figure;
                row.Gender = bot.Gender;
                row.X = bot.X;
                row.Y = bot.Y;
                row.Z = bot.Z.Value;
                row.Rotation = bot.Rotation;
                row.FreeRoam = bot.FreeRoam;
                row.ChatText = bot.ChatText;
                row.AutoChat = bot.AutoChat;
                row.ChatDelaySeconds = bot.ChatDelaySeconds;
                row.MixSentences = bot.MixSentences;
                row.DanceType = bot.DanceType;
            }

            await dbCtx.SaveChangesAsync(ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to flush {Count} dirty bots for room {RoomId}; they stay queued",
                batch.Count,
                roomId
            );
            foreach (var bot in batch)
                _state.DirtyBots.TryAdd(bot.Id, bot);

            return false;
        }
    }

    private Task FlushDirtyItemsAsync(CancellationToken ct) =>
        FlushDirtyItemsAsync(_roomConfig.MaxDirtyBatchesPerFlush, ct);

    /// <summary>
    /// Writes up to <paramref name="maxBatches"/> batches of each queue. One batch per tick
    /// left a busy room further behind on every tick. Each queue stops at its first batch that
    /// cannot be written.
    /// </summary>
    private async Task FlushDirtyItemsAsync(int maxBatches, CancellationToken ct)
    {
        await FlushDeletedItemsAsync(ct);

        for (var i = 0; i < maxBatches && _state.DirtyPets.Count > 0; i++)
        {
            if (!await FlushDirtyPetBatchAsync(ct))
                break;
        }

        for (var i = 0; i < maxBatches && _state.DirtyBots.Count > 0; i++)
        {
            if (!await FlushDirtyBotBatchAsync(ct))
                break;
        }

        for (var i = 0; i < maxBatches && _state.DirtyItems.Count > 0; i++)
        {
            if (!await FlushDirtyItemBatchAsync(ct))
                break;
        }
    }

    /// <summary>Writes one batch of furni. False when the batch could not be written.</summary>
    private async Task<bool> FlushDirtyItemBatchAsync(CancellationToken ct)
    {
        var batch = _state
            .DirtyItems.Take(_roomConfig.MaxDirtyItemsPerFlush)
            .Select(x => x.Value)
            .ToArray();

        foreach (var item in batch)
            _state.DirtyItems.Remove(item.ObjectId);

        // Read, not taken: a pickup's mark is cleared only once its write has landed.
        var removed = batch
            .Select(x => x.ObjectId)
            .Where(_state.RemovedItemIds.Contains)
            .ToHashSet();

        try
        {
            using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            foreach (var item in batch)
            {
                if (FurniIdBands.IsBuildersClub(item.ObjectId))
                {
                    FlushBorrowedItem(dbCtx, item);

                    continue;
                }

                var dbEntity = new FurnitureEntity
                {
                    Id = item.ObjectId.Value,
                    PlayerEntityId = item.OwnerId.Value,
                };

                dbCtx.Attach(dbEntity);

                var e = dbCtx.Entry(dbEntity);

                WritePlacement(e, item);

                e.Property(x => x.PlayerEntityId).IsModified = true;

                // A removed item leaves the room; anything else is written as standing in it.
                dbEntity.RoomEntityId = removed.Contains(item.ObjectId)
                    ? null
                    : _state.RoomId.Value;

                e.Property(x => x.RoomEntityId).IsModified = true;
            }

            await dbCtx.SaveChangesAsync(ct);

            // The enqueues interleave with this write: an item queued again meanwhile keeps the
            // mark its newer change gave it.
            foreach (var itemId in removed)
            {
                if (!_state.DirtyItems.ContainsKey(itemId))
                    _state.RemovedItemIds.Remove(itemId);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to flush {Count} dirty furniture items for room {RoomId}; they stay queued",
                batch.Length,
                _state.RoomId
            );

            // Queued again for the next tick, unless the room has queued something newer since.
            foreach (var item in batch)
                _state.DirtyItems.TryAdd(item.ObjectId, item);

            return false;
        }
    }

    /// <summary>
    /// Writes a borrowed furni's position back. There is no "taken out of the room but kept"
    /// state for one, because nobody owns it and it has no inventory to go to: leaving the room
    /// is the same as being given back, which the delete queue handles.
    /// </summary>
    private void FlushBorrowedItem(TurboDbContext dbCtx, RoomItemSnapshot item)
    {
        // Leaving the room is the same as being given back for a borrowed furni, which the
        // delete queue handles, so nothing should ever mark one as merely removed.
        _state.RemovedItemIds.Remove(item.ObjectId);

        var dbEntity = new BuildersClubFurnitureEntity
        {
            RoomEntityId = _state.RoomId.Value,
            RoomObjectId = item.ObjectId.Value,
            FurnitureDefinitionEntityId = item.DefinitionId,
            PlacedByPlayerEntityId = item.OwnerId.Value,
            // Only the columns marked modified are written; the rest are here to satisfy the
            // entity and never reach the database.
            CatalogOfferEntityId = 0,
        };

        dbCtx.Attach(dbEntity);

        WritePlacement(dbCtx.Entry(dbEntity), item);
    }

    /// <summary>
    /// Writes where an attached furni row stands and its data, and marks exactly those columns
    /// modified, so nothing else on the row is touched. A hotel furni and a borrowed one share
    /// these columns and were written by two copies of this block; a wall item also writes its
    /// offset. Marked by name, which EF resolves on the concrete entity.
    /// </summary>
    private static void WritePlacement<TEntity>(EntityEntry<TEntity> entry, RoomItemSnapshot item)
        where TEntity : class, IPlacedFurnitureEntity
    {
        var entity = entry.Entity;

        entity.X = item.X;
        entity.Y = item.Y;
        entity.Z = item.Z;
        entity.Rotation = item.Rotation;
        entity.ExtraData = item.ExtraData;

        entry.Property(nameof(IPlacedFurnitureEntity.X)).IsModified = true;
        entry.Property(nameof(IPlacedFurnitureEntity.Y)).IsModified = true;
        entry.Property(nameof(IPlacedFurnitureEntity.Z)).IsModified = true;
        entry.Property(nameof(IPlacedFurnitureEntity.Rotation)).IsModified = true;
        entry.Property(nameof(IPlacedFurnitureEntity.ExtraData)).IsModified = true;

        if (item is RoomWallItemSnapshot wallItem)
        {
            entity.WallOffset = wallItem.WallOffset;

            entry.Property(nameof(IPlacedFurnitureEntity.WallOffset)).IsModified = true;
        }
    }

    public async Task<bool> InsertBuildersClubItemAsync(
        RoomId roomId,
        RoomItemSnapshot snapshot,
        int offerId,
        CancellationToken ct
    )
    {
        try
        {
            using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            dbCtx.BuildersClubFurnitures.Add(
                new BuildersClubFurnitureEntity
                {
                    RoomEntityId = _state.RoomId.Value,
                    RoomObjectId = snapshot.ObjectId.Value,
                    FurnitureDefinitionEntityId = snapshot.DefinitionId,
                    PlacedByPlayerEntityId = snapshot.OwnerId.Value,
                    CatalogOfferEntityId = offerId,
                    X = snapshot.X,
                    Y = snapshot.Y,
                    Z = snapshot.Z,
                    Rotation = snapshot.Rotation,
                    WallOffset = snapshot is RoomWallItemSnapshot wallItem
                        ? wallItem.WallOffset
                        : 0,
                    ExtraData = snapshot.ExtraData,
                }
            );

            await dbCtx.SaveChangesAsync(ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to write borrowed furniture {ItemId} of offer {OfferId} in room {RoomId}",
                snapshot.ObjectId,
                offerId,
                _state.RoomId
            );

            return false;
        }
    }
}
