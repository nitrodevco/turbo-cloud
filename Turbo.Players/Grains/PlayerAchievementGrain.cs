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
using Turbo.Database.Entities.Players;
using Turbo.Players.Configuration;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums.Achievements;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Achievements;

namespace Turbo.Players.Grains;

internal sealed class PlayerAchievementGrain(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IAchievementProvider achievementProvider,
    IGrainFactory grainFactory,
    IOptions<PlayerConfig> playerConfig,
    ILogger<IPlayerAchievementGrain> logger
) : Grain, IPlayerAchievementGrain
{
    private sealed class ProgressState
    {
        public int EntityId { get; set; }
        public int Level { get; set; }
        public int Progress { get; set; }
        public bool Dirty { get; set; }
    }

    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly IAchievementProvider _achievementProvider = achievementProvider;
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly PlayerConfig _playerConfig = playerConfig.Value;
    private readonly ILogger<IPlayerAchievementGrain> _logger = logger;

    private readonly Dictionary<string, ProgressState> _progressByCode = [];
    private IDisposable? _timer;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        await HydrateAsync(ct);

        _timer = this.RegisterGrainTimer<object?>(
            static async (self, ct) =>
                await ((PlayerAchievementGrain)self!).FlushDirtyProgressAsync(ct),
            this,
            TimeSpan.FromMilliseconds(_playerConfig.AchievementProgressFlushTickMs),
            TimeSpan.FromMilliseconds(_playerConfig.AchievementProgressFlushTickMs)
        );
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        await FlushDirtyProgressAsync(ct);
    }

    public async Task<PlayerAchievementProgressSnapshot?> ProgressAsync(
        string achievementCode,
        int amount,
        CancellationToken ct
    )
    {
        if (string.IsNullOrWhiteSpace(achievementCode) || amount <= 0)
            return null;

        var definition = _achievementProvider.GetDefinition(achievementCode);

        if (definition is null || definition.Levels.Count == 0)
            return null;

        if (!_progressByCode.TryGetValue(achievementCode, out var state))
        {
            state = new ProgressState();
            _progressByCode[achievementCode] = state;
        }

        if (state.Level >= definition.MaxLevel)
            return BuildSnapshot(definition, state);

        state.Progress += amount;

        var levelUps = new List<AchievementLevelSnapshot>();

        foreach (var level in definition.Levels)
        {
            if (level.Level <= state.Level)
                continue;

            if (state.Progress < level.GoalCount)
                break;

            state.Level = level.Level;
            levelUps.Add(level);
        }

        if (levelUps.Count > 0)
        {
            try
            {
                await PersistLevelUpAsync(definition, state, ct).ConfigureAwait(false);

                state.Dirty = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to persist achievement level-up for player {PlayerId}, achievement {Code}",
                    this.GetPrimaryKeyLong(),
                    achievementCode
                );
            }

            foreach (var level in levelUps)
                await GrantLevelRewardsAsync(definition, level, state, ct).ConfigureAwait(false);
        }
        else
        {
            state.Dirty = true;
        }

        var snapshot = BuildSnapshot(definition, state);

        var playerPresence = _grainFactory.GetPlayerPresenceGrain((int)this.GetPrimaryKeyLong());

        await playerPresence.OnAchievementProgressAsync(snapshot, ct).ConfigureAwait(false);

        return snapshot;
    }

    public Task<PlayerAchievementProgressSnapshot?> GetProgressAsync(
        string achievementCode,
        CancellationToken ct
    )
    {
        var definition = _achievementProvider.GetDefinition(achievementCode);

        if (definition is null)
            return Task.FromResult<PlayerAchievementProgressSnapshot?>(null);

        _progressByCode.TryGetValue(achievementCode, out var state);

        return Task.FromResult<PlayerAchievementProgressSnapshot?>(
            BuildSnapshot(definition, state ?? new ProgressState())
        );
    }

    public Task<List<PlayerAchievementProgressSnapshot>> GetAllProgressAsync(CancellationToken ct)
    {
        var result = new List<PlayerAchievementProgressSnapshot>();

        foreach (var definition in _achievementProvider.GetAllDefinitions())
        {
            _progressByCode.TryGetValue(definition.Code, out var state);

            result.Add(BuildSnapshot(definition, state ?? new ProgressState()));
        }

        return Task.FromResult(result);
    }

    public Task<int> GetTotalScoreAsync(CancellationToken ct)
    {
        var total = 0;

        foreach (var (code, state) in _progressByCode)
        {
            var definition = _achievementProvider.GetDefinition(code);

            if (definition is null)
                continue;

            foreach (var level in definition.Levels)
            {
                if (level.Level <= state.Level)
                    total += level.ScoreReward;
            }
        }

        return Task.FromResult(total);
    }

    private async Task GrantLevelRewardsAsync(
        AchievementDefinitionSnapshot definition,
        AchievementLevelSnapshot level,
        ProgressState state,
        CancellationToken ct
    )
    {
        var playerId = (int)this.GetPrimaryKeyLong();

        if (level.CurrencyReward > 0 && level.CurrencyKind is { } currencyKind)
        {
            try
            {
                await _grainFactory
                    .GetPlayerWalletGrain(playerId)
                    .CreditAsync(currencyKind, level.CurrencyReward, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to grant achievement currency reward for player {PlayerId}, achievement {Code} level {Level}",
                    playerId,
                    definition.Code,
                    level.Level
                );
            }
        }

        if (level.ScoreReward > 0)
        {
            try
            {
                var totalScore = await GetTotalScoreAsync(ct).ConfigureAwait(false);

                await _grainFactory
                    .GetPlayerGrain(playerId)
                    .RefreshAchievementScoreAsync(totalScore, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to refresh achievement score for player {PlayerId}",
                    playerId
                );
            }
        }

        try
        {
            var playerPresence = _grainFactory.GetPlayerPresenceGrain(playerId);

            await playerPresence
                .OnAchievementLevelUpAsync(
                    new AchievementLevelUpSnapshot
                    {
                        Code = definition.Code,
                        Level = level.Level,
                        Progress = state.Progress,
                        LevelGoal = level.GoalCount,
                        BadgeCode = level.BadgeCode,
                        CurrencyKind = level.CurrencyKind,
                        CurrencyReward = level.CurrencyReward,
                        ScoreReward = level.ScoreReward,
                    },
                    ct
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to notify player {PlayerId} of achievement level-up for {Code}",
                playerId,
                definition.Code
            );
        }
    }

    private async Task PersistLevelUpAsync(
        AchievementDefinitionSnapshot definition,
        ProgressState state,
        CancellationToken ct
    )
    {
        var playerId = (int)this.GetPrimaryKeyLong();

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var entity = await dbCtx
            .PlayerAchievements.FirstOrDefaultAsync(
                x => x.PlayerEntityId == playerId && x.AchievementEntityId == definition.Id,
                ct
            )
            .ConfigureAwait(false);

        if (entity is null)
        {
            entity = new PlayerAchievementEntity
            {
                PlayerEntityId = playerId,
                AchievementEntityId = definition.Id,
                Level = state.Level,
                Progress = state.Progress,
            };

            dbCtx.PlayerAchievements.Add(entity);
        }
        else
        {
            entity.Level = state.Level;
            entity.Progress = state.Progress;
        }

        var badgeCodes = definition
            .Levels.Where(x => x.Level <= state.Level && !string.IsNullOrEmpty(x.BadgeCode))
            .Select(x => x.BadgeCode!)
            .Distinct()
            .ToList();

        if (badgeCodes.Count > 0)
        {
            var existingBadgeCodes = await dbCtx
                .PlayerBadges.Where(x =>
                    x.PlayerEntityId == playerId && badgeCodes.Contains(x.BadgeCode)
                )
                .Select(x => x.BadgeCode)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            foreach (var badgeCode in badgeCodes)
            {
                if (existingBadgeCodes.Contains(badgeCode))
                    continue;

                dbCtx.PlayerBadges.Add(
                    new PlayerBadgeEntity
                    {
                        PlayerEntityId = playerId,
                        BadgeCode = badgeCode,
                        PlayerEntity = null!,
                    }
                );
            }
        }

        await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

        state.EntityId = entity.Id;
    }

    private async Task FlushDirtyProgressAsync(CancellationToken ct)
    {
        var dirty = _progressByCode
            .Where(x => x.Value.Dirty)
            .Take(_playerConfig.MaxDirtyAchievementsPerFlush)
            .ToList();

        if (dirty.Count == 0)
            return;

        var playerId = (int)this.GetPrimaryKeyLong();

        try
        {
            await using var dbCtx = await _dbCtxFactory
                .CreateDbContextAsync(ct)
                .ConfigureAwait(false);

            foreach (var (code, state) in dirty)
            {
                var definition = _achievementProvider.GetDefinition(code);

                if (definition is null)
                    continue;

                if (state.EntityId == 0)
                {
                    var entity = new PlayerAchievementEntity
                    {
                        PlayerEntityId = playerId,
                        AchievementEntityId = definition.Id,
                        Level = state.Level,
                        Progress = state.Progress,
                    };

                    dbCtx.PlayerAchievements.Add(entity);

                    await dbCtx.SaveChangesAsync(ct).ConfigureAwait(false);

                    state.EntityId = entity.Id;
                }
                else
                {
                    await dbCtx
                        .PlayerAchievements.Where(x => x.Id == state.EntityId)
                        .ExecuteUpdateAsync(
                            up => up.SetProperty(p => p.Progress, state.Progress),
                            ct
                        )
                        .ConfigureAwait(false);
                }

                state.Dirty = false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to flush {Count} dirty achievement progress rows for player {PlayerId}",
                dirty.Count,
                playerId
            );
        }
    }

    private async Task HydrateAsync(CancellationToken ct)
    {
        _progressByCode.Clear();

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var entities = await dbCtx
            .PlayerAchievements.AsNoTracking()
            .Where(x => x.PlayerEntityId == (int)this.GetPrimaryKeyLong())
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var entity in entities)
        {
            var definition = _achievementProvider.GetDefinitionById(entity.AchievementEntityId);

            if (definition is null)
                continue;

            _progressByCode[definition.Code] = new ProgressState
            {
                EntityId = entity.Id,
                Level = entity.Level,
                Progress = entity.Progress,
            };
        }
    }

    private static PlayerAchievementProgressSnapshot BuildSnapshot(
        AchievementDefinitionSnapshot definition,
        ProgressState state
    )
    {
        var currentLevel = definition.Levels.FirstOrDefault(x => x.Level == state.Level);
        var nextLevel = definition.Levels.FirstOrDefault(x => x.Level == state.Level + 1);

        var maxLevelReached = definition.MaxLevel > 0 && state.Level >= definition.MaxLevel;

        var achievementState = maxLevelReached
            ? AchievementLevelState.MaxLevelAchieved
            : state.Level > 0
                ? AchievementLevelState.Achieved
                : AchievementLevelState.RequirementsNotMet;

        return new PlayerAchievementProgressSnapshot
        {
            AchievementId = definition.Id,
            Code = definition.Code,
            Category = definition.Category,
            Level = state.Level,
            MaxLevel = definition.MaxLevel,
            Progress = state.Progress,
            LevelGoal = currentLevel?.GoalCount ?? 0,
            NextLevelGoal = nextLevel?.GoalCount ?? currentLevel?.GoalCount ?? 0,
            State = achievementState,
        };
    }
}
