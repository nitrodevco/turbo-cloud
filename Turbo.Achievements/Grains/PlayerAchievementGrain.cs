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
using Turbo.Primitives.Achievements.Enums;
using Turbo.Primitives.Achievements.Grains;
using Turbo.Primitives.Achievements.Snapshots;
using Turbo.Primitives.Orleans;

namespace Turbo.Achievements.Grains;

/// <summary>
/// Write-through progression. Reads are intentionally lazy: recovery activates offline players
/// for facts alone. Progress, crossed awards and admission receipts commit in one unit of work.
/// This grain awaits neither a gameplay owner nor any caller that waits on it.
/// It is split by concern: <c>.Bindings</c> resolves what a fact is evaluated against,
/// <c>.Awards</c> plans and delivers awards and keeps the totals, <c>.Presentation</c> tells the
/// client and <c>.Administration</c> holds the audited operator actions.
/// </summary>
internal sealed partial class PlayerAchievementGrain : Grain, IPlayerAchievementGrain
{
    private readonly IDbContextFactory<TurboDbContext> _database;
    private readonly AchievementConfig _config;
    private readonly IGrainFactory _grains;
    private readonly IAchievementCatalog _catalog;
    private readonly IAchievementFactRecorder _recorder;
    private readonly IAchievementRewardRegistry _rewards;
    private readonly IAchievementObserverRegistry _observers;
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
        IAchievementObserverRegistry observers,
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
        _observers = observers;
        _evaluator = evaluator;
        _logger = logger;
        _state = new() { PlayerId = this.GetPlayerId() };
    }

    /// <summary>Read-only: login and the recovery pass keep progress current, so this writes nothing.</summary>
    public async Task<ImmutableArray<AchievementSnapshot>> GetAchievementsAsync(
        CancellationToken ct
    )
    {
        await using var db = await _database.CreateDbContextAsync(ct);
        var progress = await db
            .AchievementProgress.AsNoTracking()
            .Where(x => x.PlayerId == _state.PlayerId.Value)
            .ToDictionaryAsync(x => x.AchievementId, ct);
        return _catalog
            .Current.Where(x =>
                x.IsListedFor(
                    _config.ArchiveShowsAll
                        || (
                            progress.TryGetValue(x.Id, out var seen)
                            && (seen.Value > 0 || seen.EarnedLevel > 0)
                        )
                )
            )
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
            var revisions = new Dictionary<(int, int), AchievementDefinition>();
            foreach (var admitted in facts)
            {
                var fact =
                    JsonSerializer.Deserialize<AchievementFact>(admitted.FactJson)
                    ?? throw new InvalidOperationException("Invalid admitted fact.");
                var bindings = await ResolveBindingsAsync(db, admitted.BindingsJson, revisions, ct);
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
                    if (definition.Reducer == AchievementReducer.Distinct)
                        await ApplyDistinctAsync(db, progress, fact, ct);
                    else
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

    public Task ReconcileAsync(CancellationToken ct) => ReconcileCoreAsync(force: false, ct);

    /// <summary>
    /// Brings a player's progress in line with the catalog and the authoritative state. The badge
    /// cleanup and the award re-evaluation run only when the catalog revisions changed since this
    /// player was last evaluated (or when forced by an operator); every other call records only the
    /// state values that moved and processes whatever is pending.
    /// </summary>
    private async Task ReconcileCoreAsync(bool force, CancellationToken ct)
    {
        var stamp = AchievementCatalogStamp.Of(_catalog.Current);
        var stale = force;
        await using (var db = await _database.CreateDbContextAsync(ct))
        {
            var projection = await db.AchievementProjections.FindAsync([_state.PlayerId.Value], ct);
            stale |= projection is null || projection.ReconciledStamp != stamp;
            if (stale)
            {
                // Older accounts own every level they ever reached; only the current one stays.
                await _grains
                    .GetPlayerBadgeGrain(_state.PlayerId)
                    .NormalizeAchievementBadgesAsync(
                        [
                            .. _catalog
                                .Current.Where(x => x.Levels.Length > 0)
                                .Select(x => AchievementBadgeCodes.BaseOf(x.Levels[0].BadgeCode))
                                .Distinct(),
                        ],
                        ct
                    );
                // Re-evaluate retained progress under a new compatible catalog, without inventing actions.
                var progress = await db
                    .AchievementProgress.Where(x => x.PlayerId == _state.PlayerId.Value)
                    .ToDictionaryAsync(x => x.AchievementId, ct);
                foreach (var definition in _catalog.Current.Where(x => x.Accrues()))
                    if (progress.TryGetValue(definition.Id, out var value))
                        await CreateAwardsAsync(db, definition, value, DateTime.UtcNow, ct);
                if (projection is null)
                {
                    projection = new() { PlayerId = _state.PlayerId.Value };
                    db.AchievementProjections.Add(projection);
                }
                projection.ReconciledStamp = stamp;
                await RecomputeTotalsAsync(db, projection, ct);
                await db.SaveChangesAsync(ct);
            }
        }
        // A catalog the player has not seen yet has not seen the current state values either.
        await _evaluator.RecordAsync(_state.PlayerId, ct, force: stale);
        await ProcessAsync(ct);
    }

    public async Task<ImmutableArray<AchievementAwardStatusSnapshot>> GetPendingAwardsAsync(
        CancellationToken ct
    )
    {
        await using var db = await _database.CreateDbContextAsync(ct);
        var pending = await db
            .AchievementProgress.AsNoTracking()
            .Where(x => x.PlayerId == _state.PlayerId.Value && x.PendingDelivery)
            .OrderBy(x => x.AchievementId)
            .ToListAsync(ct);
        return
        [
            .. pending.SelectMany(progress =>
                progress
                    .ReadOpenAwards()
                    .Where(x => !x.Completed)
                    .Select(x => new AchievementAwardStatusSnapshot
                    {
                        AwardKey = AchievementOpenAwards.AwardKey(
                            progress.PlayerId,
                            progress.AchievementId,
                            x.Level
                        ),
                        DeliveredRewards = x.DeliveredRewards,
                        BlockedReason = x.BlockedReason,
                    })
            ),
        ];
    }

    public Task RetryAsync(CancellationToken ct) => ProcessAsync(ct);
}
