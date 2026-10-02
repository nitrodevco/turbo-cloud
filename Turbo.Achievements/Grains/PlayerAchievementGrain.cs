using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Achievements.Configuration;
using Turbo.Database.Achievements;
using Turbo.Database.Context;
using Turbo.Database.Entities.Achievements;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Grains;
using Turbo.Primitives.Achievements.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums.Wallet;

namespace Turbo.Achievements.Grains;

/// <summary>
/// Write-through progression. Reads are intentionally lazy: recovery activates offline players
/// for facts alone. Progress, crossed awards and admission receipts commit in one unit of work.
/// This grain awaits neither a gameplay owner nor any caller that waits on it.
/// </summary>
internal sealed class PlayerAchievementGrain : Grain, IPlayerAchievementGrain
{
    private readonly IDbContextFactory<TurboDbContext> _database;
    private readonly AchievementConfig _config;
    private readonly IGrainFactory _grains;
    private readonly IAchievementCatalog _catalog;
    private readonly IAchievementFactRecorder _recorder;
    private readonly IAchievementRewardRegistry _rewards;
    private readonly AchievementStateEvaluator _evaluator;
    private readonly ILogger<IPlayerAchievementGrain> _logger;
    private readonly PlayerAchievementLiveState _state;

    public PlayerAchievementGrain(
        IDbContextFactory<TurboDbContext> database,
        IOptions<AchievementConfig> config,
        IGrainFactory grains,
        IAchievementCatalog catalog,
        IAchievementFactRecorder recorder,
        IAchievementRewardRegistry rewards,
        AchievementStateEvaluator evaluator,
        ILogger<IPlayerAchievementGrain> logger
    )
    {
        _database = database;
        _config = config.Value;
        _grains = grains;
        _catalog = catalog;
        _recorder = recorder;
        _rewards = rewards;
        _evaluator = evaluator;
        _logger = logger;
        _state = new() { PlayerId = this.GetPlayerId() };
    }

    public async Task<ImmutableArray<AchievementSnapshot>> GetAchievementsAsync(
        CancellationToken ct
    )
    {
        await ReconcileAsync(ct);
        await using var db = await _database.CreateDbContextAsync(ct);
        var progress = await db
            .AchievementProgress.AsNoTracking()
            .Where(x => x.PlayerId == _state.PlayerId.Value)
            .ToDictionaryAsync(x => x.AchievementId, ct);
        return _catalog
            .Current.Where(x => x.Enabled || x.Archived)
            .Select(x => AchievementProjection.ToSnapshot(x, progress.GetValueOrDefault(x.Id)))
            .ToImmutableArray();
    }

    public async Task ProcessAsync(CancellationToken ct)
    {
        await using (var pendingDb = await _database.CreateDbContextAsync(ct))
        {
            if (
                await pendingDb.AchievementFacts.AnyAsync(
                    x =>
                        x.PlayerId == _state.PlayerId.Value
                        && !x.Processed
                        && !x.OperationId.StartsWith("state:")
                        && (
                            x.Source == AchievementSources.HC
                            || x.Source == AchievementSources.ONLINE
                            || x.Source == AchievementSources.LOGIN
                        ),
                    ct
                )
            )
                await _evaluator.RecordAsync(_state.PlayerId, ct);
        }
        await using (var db = await _database.CreateDbContextAsync(ct))
        {
            var facts = await db
                .AchievementFacts.Where(x => x.PlayerId == _state.PlayerId.Value && !x.Processed)
                .OrderBy(x => x.Id)
                .Take(_config.FactBatchSize)
                .ToListAsync(ct);
            foreach (var admitted in facts)
            {
                var fact =
                    JsonSerializer.Deserialize<AchievementFact>(admitted.FactJson)
                    ?? throw new InvalidOperationException("Invalid admitted fact.");
                var bindings =
                    JsonSerializer.Deserialize<AchievementDefinition[]>(admitted.BindingsJson)
                    ?? [];
                foreach (var definition in bindings)
                {
                    var progress = await db.AchievementProgress.FindAsync(
                        [_state.PlayerId.Value, definition.Id],
                        ct
                    );
                    if (progress is null)
                    {
                        progress = new()
                        {
                            PlayerId = _state.PlayerId.Value,
                            AchievementId = definition.Id,
                        };
                        db.AchievementProgress.Add(progress);
                    }
                    AchievementReducerEngine.Apply(
                        progress,
                        definition,
                        fact,
                        _config.MaxDistinctValues
                    );
                    await CreateAwardsAsync(db, definition, progress, fact.OccurredAtUtc, ct);
                }
                admitted.Processed = true;
                // No in-memory state advances on failure. All rows for this fact commit together.
                await db.SaveChangesAsync(ct);
            }
        }
        await DeliverAwardsAsync(ct);
        await PublishAsync(ct);
    }

    public async Task<ImmutableArray<AchievementAwardStatusSnapshot>> GetPendingAwardsAsync(
        CancellationToken ct
    )
    {
        await using var db = await _database.CreateDbContextAsync(ct);
        var pending = await db
            .AchievementAwards.AsNoTracking()
            .Where(x => x.PlayerId == _state.PlayerId.Value && !x.Completed)
            .OrderBy(x => x.AchievementId)
            .ThenBy(x => x.Level)
            .ToListAsync(ct);
        return pending
            .Select(x => new AchievementAwardStatusSnapshot
            {
                AwardKey = x.AwardKey,
                DeliveredRewards = x.DeliveredRewards,
                BlockedReason = x.BlockedReason,
            })
            .ToImmutableArray();
    }

    private static async Task CreateAwardsAsync(
        TurboDbContext db,
        AchievementDefinition definition,
        AchievementProgressEntity progress,
        DateTime earnedAt,
        CancellationToken ct
    )
    {
        // Older catalogs recorded zero-valued HC state for non-members. A joining
        // level must also verify membership when retained progress is reconciled.
        if (
            definition.Source == AchievementSources.HC
            && definition.Levels[0].Requirement == 0
            && !await db.AchievementMembershipIntervals.AnyAsync(
                x =>
                    x.PlayerId == progress.PlayerId
                    && x.StartUtc <= earnedAt
                    && x.EndUtc > x.StartUtc,
                ct
            )
        )
            return;
        for (var index = 0; index < definition.Levels.Length; index++)
        {
            if (
                !AchievementReducerEngine.Qualifies(
                    definition,
                    definition.Levels[index],
                    progress.Value
                )
            )
                break;
            var level = index + 1;
            if (level <= progress.EarnedLevel)
                continue;
            if (
                !await db.AchievementAwards.AnyAsync(
                    x =>
                        x.PlayerId == progress.PlayerId
                        && x.AchievementId == definition.Id
                        && x.Level == level,
                    ct
                )
            )
                db.AchievementAwards.Add(
                    new()
                    {
                        PlayerId = progress.PlayerId,
                        AchievementId = definition.Id,
                        Level = level,
                        Revision = definition.Revision,
                        AwardKey = $"achievement:{progress.PlayerId}:{definition.Id}:{level}",
                        DefinitionJson = JsonSerializer.Serialize(definition),
                        RewardJson = JsonSerializer.Serialize(definition.Levels[index]),
                        EarnedAtUtc = earnedAt,
                    }
                );
            progress.EarnedLevel = level;
        }
    }

    private async Task DeliverAwardsAsync(CancellationToken ct)
    {
        await using var db = await _database.CreateDbContextAsync(ct);
        var pending = await db
            .AchievementAwards.Where(x => x.PlayerId == _state.PlayerId.Value && !x.Completed)
            .OrderBy(x => x.AchievementId)
            .ThenBy(x => x.Level)
            .ToListAsync(ct);
        foreach (var achievement in pending.GroupBy(x => x.AchievementId))
        foreach (var award in achievement)
        {
            try
            {
                if (db.Entry(award).State == EntityState.Detached)
                    db.Attach(award);
                var level =
                    JsonSerializer.Deserialize<AchievementLevelDefinition>(award.RewardJson)
                    ?? throw new InvalidOperationException("Missing frozen reward.");
                while (award.DeliveredRewards < level.Rewards.Length)
                {
                    var reward = level.Rewards[award.DeliveredRewards];
                    var receiptKey = $"{award.AwardKey}:reward:{award.DeliveredRewards}";
                    if (reward.Handler == "wallet" && reward.Version == 1)
                    {
                        if (
                            reward.Currency is not { } currency
                            || !await _grains
                                .GetPlayerWalletGrain(_state.PlayerId)
                                .CreditAchievementAsync(receiptKey, currency, reward.Amount, ct)
                        )
                            throw new InvalidOperationException(
                                "Wallet reward blocked: unknown currency or overflow."
                            );
                    }
                    else if (_rewards.TryGet(reward.Handler, reward.Version, out var handler))
                        await handler.DeliverAsync(_state.PlayerId, receiptKey, reward, ct);
                    else
                        throw new InvalidOperationException(
                            $"Reward handler unavailable: {reward.Handler}/{reward.Version}."
                        );
                    award.DeliveredRewards++;
                    await db.SaveChangesAsync(ct);
                }
                await _grains
                    .GetPlayerBadgeGrain(_state.PlayerId)
                    .GrantAchievementAsync(award.AchievementId, award.Level, level.BadgeCode, ct);
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                award.Completed = true;
                award.BlockedReason = null;
                await db.SaveChangesAsync(ct);
                var completed = await db
                    .AchievementAwards.Where(x =>
                        x.PlayerId == _state.PlayerId.Value && x.Completed
                    )
                    .ToListAsync(ct);
                var projection = await db.AchievementProjections.FindAsync(
                    [_state.PlayerId.Value],
                    ct
                );
                if (projection is null)
                {
                    projection = new() { PlayerId = _state.PlayerId.Value };
                    db.AchievementProjections.Add(projection);
                }
                projection.Score = checked(
                    completed.Sum(x =>
                        JsonSerializer.Deserialize<AchievementLevelDefinition>(x.RewardJson)!.Score
                    )
                );
                projection.EarnedLevels = checked(
                    completed.GroupBy(x => x.AchievementId).Sum(x => x.Max(a => a.Level))
                );
                projection.PublicationPending = true;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Each achievement is ordered; unrelated awards continue after this group stops.
                _logger.LogError(ex, "Achievement award {AwardKey} blocked", award.AwardKey);
                // A rollback can leave tracked changes ahead of the database.
                db.ChangeTracker.Clear();
                var blocked = await db.AchievementAwards.FindAsync(
                    [award.PlayerId, award.AchievementId, award.Level],
                    ct
                );
                if (blocked is not null && !blocked.Completed)
                {
                    blocked.BlockedReason =
                        ex.Message.Length > 512 ? ex.Message[..512] : ex.Message;
                    await db.SaveChangesAsync(ct);
                }
                break;
            }
        }
    }

    private async Task PublishAsync(CancellationToken ct)
    {
        await using var db = await _database.CreateDbContextAsync(ct);
        var projection = await db.AchievementProjections.FindAsync([_state.PlayerId.Value], ct);
        if (projection is not null && projection.PublicationPending)
        {
            await _grains
                .GetPlayerGrain(_state.PlayerId)
                .SetAchievementTotalsAsync(projection.Score, projection.EarnedLevels, ct);
            await _grains.SendComposerToPlayerAsync(
                _state.PlayerId,
                new AchievementsScoreEventMessageComposer { Score = projection.Score },
                ct
            );
            projection.PublicationPending = false;
            await db.SaveChangesAsync(ct);
        }
        var presentations = await db
            .AchievementAwards.Where(x =>
                x.PlayerId == _state.PlayerId.Value && x.Completed && !x.Presented
            )
            .OrderBy(x => x.AchievementId)
            .ThenBy(x => x.Level)
            .ToListAsync(ct);
        foreach (var award in presentations)
        {
            var definition = JsonSerializer.Deserialize<AchievementDefinition>(
                award.DefinitionJson
            )!;
            var level = JsonSerializer.Deserialize<AchievementLevelDefinition>(award.RewardJson)!;
            var badge = await _grains
                .GetPlayerBadgeGrain(_state.PlayerId)
                .GetBadgeSnapshotAsync(level.BadgeCode, ct);
            var activity = level.Rewards.FirstOrDefault(x =>
                x.Currency?.CurrencyType == CurrencyType.ActivityPoints
            );
            var delivered = await _grains.TrySendComposerToPlayerAsync(
                _state.PlayerId,
                new HabboAchievementNotificationMessageComposer
                {
                    Type = 1,
                    Level = award.Level,
                    BadgeId = badge?.BadgeId ?? 0,
                    BadgeCode = level.BadgeCode,
                    PointsTotal = definition.Levels.Take(award.Level).Sum(x => x.Score),
                    LevelRewardPoints = activity?.Amount ?? 0,
                    LevelRewardPointType = activity?.Currency?.ActivityPointType ?? 0,
                    BonusPoints = 0,
                    AchievementId = award.AchievementId,
                    RemovedBadgeCode =
                        award.Level > 1 ? definition.Levels[award.Level - 2].BadgeCode : "",
                    Category = definition.Category,
                    ShowDialogToUser = _config.ShowCongratulationsDialog,
                    OwnerCount = badge?.OwnerCount ?? 0,
                    BadgeRarityId = badge is null ? 0 : (int)badge.Rarity,
                },
                ct
            );
            if (!delivered)
                break;
            var progress = await db.AchievementProgress.FindAsync(
                [_state.PlayerId.Value, award.AchievementId],
                ct
            );
            await _grains.SendComposerToPlayerAsync(
                _state.PlayerId,
                new AchievementEventMessageComposer
                {
                    Achievement = AchievementProjection.ToSnapshot(
                        _catalog.Current.FirstOrDefault(x => x.Id == award.AchievementId)
                            ?? definition,
                        progress
                    ),
                },
                ct
            );
            award.Presented = true;
            await db.SaveChangesAsync(ct);
        }
    }

    public async Task ReconcileAsync(CancellationToken ct)
    {
        await _evaluator.RecordAsync(_state.PlayerId, ct);
        // Re-evaluate retained progress under a new compatible catalog, without inventing actions.
        await using (var db = await _database.CreateDbContextAsync(ct))
        {
            var progress = await db
                .AchievementProgress.Where(x => x.PlayerId == _state.PlayerId.Value)
                .ToDictionaryAsync(x => x.AchievementId, ct);
            foreach (var definition in _catalog.Current.Where(x => x.Enabled && !x.Archived))
                if (progress.TryGetValue(definition.Id, out var value))
                    await CreateAwardsAsync(db, definition, value, DateTime.UtcNow, ct);
            await db.SaveChangesAsync(ct);
        }
        await ProcessAsync(ct);
    }

    public async Task AdvanceAsync(
        int achievementId,
        long progress,
        string actor,
        string reason,
        string operationId,
        CancellationToken ct
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (operationId.Length > 160)
            throw new ArgumentException("Operation id too long.", nameof(operationId));
        var definition = _catalog.Current.Single(x => x.Id == achievementId);
        if (definition.Reducer == Turbo.Primitives.Achievements.Enums.AchievementReducer.Rank)
            throw new InvalidOperationException(
                "Rank criteria reconcile from authoritative rankings."
            );
        await using (var db = await _database.CreateDbContextAsync(ct))
        {
            var request = JsonSerializer.Serialize(
                new
                {
                    Action = "advance",
                    PlayerId = _state.PlayerId.Value,
                    AchievementId = achievementId,
                    Progress = progress,
                }
            );
            var receipt = await db.AchievementAudit.SingleOrDefaultAsync(
                x => x.OperationId == operationId,
                ct
            );
            if (receipt is not null)
            {
                if (
                    receipt.Actor != actor
                    || receipt.Reason != reason
                    || receipt.RequestJson != request
                )
                    throw new InvalidOperationException("Audit operation id collision.");
                return;
            }
            var row = await db.AchievementProgress.FindAsync(
                [_state.PlayerId.Value, achievementId],
                ct
            );
            if (row is null)
            {
                row = new() { PlayerId = _state.PlayerId.Value, AchievementId = achievementId };
                db.AchievementProgress.Add(row);
            }
            if (progress < row.Value)
                throw new InvalidOperationException(
                    "Achievement administration only advances progress."
                );
            var before = JsonSerializer.Serialize(row);
            if (
                definition.Reducer
                is Turbo.Primitives.Achievements.Enums.AchievementReducer.Distinct
                    or Turbo.Primitives.Achievements.Enums.AchievementReducer.ElapsedSeconds
            )
                row.ForwardAdjustment = checked(row.ForwardAdjustment + progress - row.Value);
            row.Value = progress;
            await CreateAwardsAsync(db, definition, row, DateTime.UtcNow, ct);
            db.AchievementAudit.Add(
                new()
                {
                    OperationId = operationId,
                    Actor = actor,
                    Reason = reason,
                    RequestJson = request,
                    OccurredAtUtc = DateTime.UtcNow,
                    BeforeJson = before,
                    AfterJson = JsonSerializer.Serialize(row),
                }
            );
            await db.SaveChangesAsync(ct);
        }
        await ProcessAsync(ct);
    }

    public Task RetryAsync(CancellationToken ct) => ProcessAsync(ct);

    public async Task AdministerAsync(
        bool retryOnly,
        string actor,
        string reason,
        string operationId,
        CancellationToken ct
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actor);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);
        if (operationId.Length > 160)
            throw new ArgumentException("Operation id too long.", nameof(operationId));
        await using var db = await _database.CreateDbContextAsync(ct);
        var audit = await db.AchievementAudit.SingleOrDefaultAsync(
            x => x.OperationId == operationId,
            ct
        );
        var request = JsonSerializer.Serialize(
            new { Action = retryOnly ? "retry" : "reconcile", PlayerId = _state.PlayerId.Value }
        );
        if (
            audit is not null
            && (audit.Actor != actor || audit.Reason != reason || audit.RequestJson != request)
        )
            throw new InvalidOperationException("Audit operation id collision.");
        if (audit is not null && audit.AfterJson != "pending")
            return;
        if (audit is null)
        {
            audit = new()
            {
                OperationId = operationId,
                Actor = actor,
                Reason = reason,
                RequestJson = request,
                OccurredAtUtc = DateTime.UtcNow,
                BeforeJson = JsonSerializer.Serialize(
                    await db
                        .AchievementProgress.AsNoTracking()
                        .Where(x => x.PlayerId == _state.PlayerId.Value)
                        .ToListAsync(ct)
                ),
                AfterJson = "pending",
            };
            db.AchievementAudit.Add(audit);
            await db.SaveChangesAsync(ct);
        }
        if (retryOnly)
            await ProcessAsync(ct);
        else
            await ReconcileAsync(ct);
        audit.AfterJson = JsonSerializer.Serialize(
            await db
                .AchievementProgress.AsNoTracking()
                .Where(x => x.PlayerId == _state.PlayerId.Value)
                .ToListAsync(ct)
        );
        await db.SaveChangesAsync(ct);
    }
}
