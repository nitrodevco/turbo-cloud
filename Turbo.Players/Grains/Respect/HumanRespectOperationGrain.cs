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
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Grains.Respect;

namespace Turbo.Players.Grains.Respect;

/// <summary>
/// Gives one respect, keyed by its id (<c>player_respects.id</c>), across the two player grains.
/// The respect is written as pending first; the giver's grain spends it and the receiver's grain
/// credits it, each recording that change with its own write; then it is marked given with the
/// achievement facts for both. Run again with the same id it finishes what is left, or answers
/// with how it ended.
/// </summary>
internal sealed class HumanRespectOperationGrain : Grain, IHumanRespectOperationGrain
{
    private readonly IDbContextFactory<TurboDbContext> _database;
    private readonly IGrainFactory _grains;
    private readonly IAchievementFactRecorder _facts;
    private readonly ILogger<IHumanRespectOperationGrain> _logger;
    private readonly Guid _respectId;

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
        _respectId = this.GetPrimaryKey();
    }

    public async Task<HumanRespectOperationResult> ExecuteAsync(
        PlayerId actorId,
        PlayerId targetId,
        CancellationToken ct
    )
    {
        var respectId = _respectId;
        ValidateRequest(respectId, actorId, targetId);

        await using (var db = await _database.CreateDbContextAsync(ct))
        {
            var respect = await db.PlayerRespects.SingleOrDefaultAsync(x => x.Id == respectId, ct);
            if (respect is null)
            {
                respect = new PlayerRespectEntity
                {
                    Id = respectId,
                    GiverId = actorId.Value,
                    ReceiverId = targetId.Value,
                    Status = RespectStatus.Pending,
                };
                db.PlayerRespects.Add(respect);
                await db.SaveChangesAsync(ct);
            }
            else
            {
                EnsureSameParticipants(respect, actorId, targetId);
                if (respect.Status == RespectStatus.Refused)
                    return new() { Accepted = false };
                if (respect.Status == RespectStatus.Given)
                    return new() { Accepted = true, TargetRespectTotal = respect.ReceiverTotal };
            }
        }

        var spent = await _grains.GetPlayerGrain(actorId).SpendRespectOperationAsync(respectId, ct);
        if (!spent)
        {
            await using var db = await _database.CreateDbContextAsync(ct);
            var respect = await GetRespectAsync(db, respectId, actorId, targetId, ct);
            if (respect.Status == RespectStatus.Given)
                return new() { Accepted = true, TargetRespectTotal = respect.ReceiverTotal };
            respect.Status = RespectStatus.Refused;
            await db.SaveChangesAsync(ct);
            _logger.LogWarning(
                "Respect {RespectId} from player {ActorId} to player {TargetId} was refused because no respect remained",
                respectId,
                actorId,
                targetId
            );
            return new() { Accepted = false };
        }

        var total = await _grains
            .GetPlayerGrain(targetId)
            .ReceiveRespectOperationAsync(respectId, ct);

        await using (var db = await _database.CreateDbContextAsync(ct))
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var respect = await GetRespectAsync(db, respectId, actorId, targetId, ct);
            if (respect.Status == RespectStatus.Refused)
                throw new InvalidOperationException("A spent respect is marked refused.");
            if (respect.Status == RespectStatus.Given)
                return new() { Accepted = true, TargetRespectTotal = respect.ReceiverTotal };

            var occurredAt = DateTime.UtcNow;
            _facts.Record(
                db,
                actorId,
                new AchievementFact
                {
                    OperationId = $"human-respect:{respectId:N}:given",
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
                    OperationId = $"human-respect:{respectId:N}:received",
                    Source = AchievementSources.RESPECT_RECEIVED,
                    OccurredAtUtc = occurredAt,
                    Amount = 1,
                }
            );
            respect.Status = RespectStatus.Given;
            respect.ReceiverTotal = total;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        return new() { Accepted = true, TargetRespectTotal = total };
    }

    private static void ValidateRequest(Guid respectId, PlayerId actorId, PlayerId targetId)
    {
        if (respectId == Guid.Empty)
            throw new ArgumentException("A respect needs an id.", nameof(respectId));
        if (actorId.Value <= 0 || targetId.Value <= 0 || actorId == targetId)
            throw new ArgumentException("Respect requires two distinct valid players.");
    }

    private static void EnsureSameParticipants(
        PlayerRespectEntity respect,
        PlayerId actorId,
        PlayerId targetId
    )
    {
        if (respect.GiverId != actorId.Value || respect.ReceiverId != targetId.Value)
            throw new InvalidOperationException("The respect id is already another pair's.");
    }

    private static async Task<PlayerRespectEntity> GetRespectAsync(
        TurboDbContext db,
        Guid respectId,
        PlayerId actorId,
        PlayerId targetId,
        CancellationToken ct
    )
    {
        var respect = await db.PlayerRespects.SingleAsync(x => x.Id == respectId, ct);
        EnsureSameParticipants(respect, actorId, targetId);
        return respect;
    }
}
