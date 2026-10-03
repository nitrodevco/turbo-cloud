using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Achievements;
using Turbo.Database.Context;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Grains.Respect;

namespace Turbo.Players.Grains.Respect;

/// <summary>
/// Coordinates a human respect across the two player grains. Admission, rejection or completion,
/// and the paired achievement facts are written through; the participant grains persist their
/// idempotent debit and credit receipts with their own player mutations.
/// </summary>
internal sealed class HumanRespectOperationGrain : Grain, IHumanRespectOperationGrain
{
    private const int MAX_OPERATION_ID_LENGTH = 100;

    private readonly IDbContextFactory<TurboDbContext> _database;
    private readonly IGrainFactory _grains;
    private readonly IAchievementFactRecorder _facts;
    private readonly ILogger<IHumanRespectOperationGrain> _logger;
    private readonly string _operationId;

    public HumanRespectOperationGrain(
        IDbContextFactory<TurboDbContext> database,
        IGrainFactory grains,
        IAchievementFactRecorder facts,
        ILogger<IHumanRespectOperationGrain> logger
    )
    {
        _database = database;
        _grains = grains;
        _facts = facts;
        _logger = logger;
        _operationId = this.GetPrimaryKeyString();
    }

    public async Task<HumanRespectOperationResult> ExecuteAsync(
        PlayerId actorId,
        PlayerId targetId,
        CancellationToken ct
    )
    {
        var operationId = _operationId;
        ValidateRequest(operationId, actorId, targetId);

        await using (var db = await _database.CreateDbContextAsync(ct))
        {
            var operation = await db.HumanRespectOperations.SingleOrDefaultAsync(
                x => x.OperationId == operationId,
                ct
            );
            if (operation is null)
            {
                operation = new HumanRespectOperationEntity
                {
                    OperationId = operationId,
                    ActorId = actorId.Value,
                    TargetId = targetId.Value,
                };
                db.HumanRespectOperations.Add(operation);
                await db.SaveChangesAsync(ct);
            }
            else
            {
                EnsureSameParticipants(operation, actorId, targetId);
                if (operation.Rejected)
                    return new() { Accepted = false };
                if (operation.Completed)
                    return new() { Accepted = true, TargetRespectTotal = operation.ResultTotal };
            }
        }

        var spent = await _grains
            .GetPlayerGrain(actorId)
            .SpendRespectOperationAsync(operationId, ct);
        if (!spent)
        {
            await using var db = await _database.CreateDbContextAsync(ct);
            var operation = await GetOperationAsync(db, operationId, actorId, targetId, ct);
            if (operation.Completed)
                return new() { Accepted = true, TargetRespectTotal = operation.ResultTotal };
            operation.Rejected = true;
            await db.SaveChangesAsync(ct);
            _logger.LogWarning(
                "Respect operation {OperationId} from player {ActorId} to player {TargetId} was rejected because no respect remained",
                operationId,
                actorId,
                targetId
            );
            return new() { Accepted = false };
        }

        var total = await _grains
            .GetPlayerGrain(targetId)
            .ReceiveRespectOperationAsync(operationId, ct);

        await using (var db = await _database.CreateDbContextAsync(ct))
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var operation = await GetOperationAsync(db, operationId, actorId, targetId, ct);
            if (operation.Rejected)
                throw new InvalidOperationException(
                    "A spent respect operation is marked rejected."
                );
            if (operation.Completed)
                return new() { Accepted = true, TargetRespectTotal = operation.ResultTotal };

            var occurredAt = DateTime.UtcNow;
            _facts.Record(
                db,
                actorId,
                new AchievementFact
                {
                    OperationId = $"human-respect:{operationId}:given",
                    Source = AchievementSources.RESPECT_GIVEN,
                    OccurredAtUtc = occurredAt,
                    Amount = 1,
                }
            );
            _facts.Record(
                db,
                targetId,
                new AchievementFact
                {
                    OperationId = $"human-respect:{operationId}:received",
                    Source = AchievementSources.RESPECT_RECEIVED,
                    OccurredAtUtc = occurredAt,
                    Amount = 1,
                }
            );
            operation.Completed = true;
            operation.ResultTotal = total;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        return new() { Accepted = true, TargetRespectTotal = total };
    }

    private static void ValidateRequest(string operationId, PlayerId actorId, PlayerId targetId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (operationId.Length > MAX_OPERATION_ID_LENGTH)
            throw new ArgumentException("Respect operation id is too long.", nameof(operationId));
        if (actorId.Value <= 0 || targetId.Value <= 0 || actorId == targetId)
            throw new ArgumentException("Respect requires two distinct valid players.");
    }

    private static void EnsureSameParticipants(
        HumanRespectOperationEntity operation,
        PlayerId actorId,
        PlayerId targetId
    )
    {
        if (operation.ActorId != actorId.Value || operation.TargetId != targetId.Value)
            throw new InvalidOperationException(
                "Respect operation id is already bound to other players."
            );
    }

    private static async Task<HumanRespectOperationEntity> GetOperationAsync(
        TurboDbContext db,
        string operationId,
        PlayerId actorId,
        PlayerId targetId,
        CancellationToken ct
    )
    {
        var operation = await db.HumanRespectOperations.SingleAsync(
            x => x.OperationId == operationId,
            ct
        );
        EnsureSameParticipants(operation, actorId, targetId);
        return operation;
    }
}
