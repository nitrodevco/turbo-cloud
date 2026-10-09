using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Achievements.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Quests;
using Turbo.Database.Extensions;
using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Quests.Enums;
using Turbo.Primitives.Quests.Grains;
using Turbo.Primitives.Quests.Snapshots;

namespace Turbo.Achievements.Grains;

/// <summary>
/// A player's daily tasks, write-through: each assignment, step and claim is saved before it is
/// sent. Reading is lazy and per task day rather than at activation: the grain is woken by every
/// room visit and furni use, and the day's tasks are only worth reading once one of them can
/// count, so the first call of each day reads the definitions and the player's tasks again and
/// gives the day's tasks if the player has none.
/// </summary>
internal sealed class PlayerDailyTaskGrain : Grain, IPlayerDailyTaskGrain
{
    private const string CREDIT_REFERENCE_PREFIX = "dailytask:";

    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly DailyTaskConfig _config;
    private readonly IGrainFactory _grainFactory;
    private readonly TimeProvider _time;
    private readonly ILogger<IPlayerDailyTaskGrain> _logger;

    private readonly PlayerDailyTaskLiveState _state;

    public PlayerDailyTaskGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<DailyTaskConfig> config,
        IGrainFactory grainFactory,
        TimeProvider time,
        ILogger<IPlayerDailyTaskGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _config = config.Value;
        _grainFactory = grainFactory;
        _time = time;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
    }

    public async Task SendTasksAsync(CancellationToken ct)
    {
        var now = Now;
        await EnsureDayAsync(now, ct);

        await _grainFactory.SendComposerToPlayerAsync(
            _state.PlayerId,
            new DailyTasksActiveListMessageComposer { Tasks = Snapshots(_state.Tasks, now) },
            ct
        );
    }

    public async Task<bool> ClaimAsync(long taskId, CancellationToken ct)
    {
        var now = Now;
        await EnsureDayAsync(now, ct);

        var task = _state.Tasks.Find(x => x.Id == taskId);

        if (task is null || task.Status != DailyTaskStatus.Completed)
        {
            _logger.LogWarning(
                "Rejected daily task claim {TaskId} for player {PlayerId}: {Reason}",
                taskId,
                _state.PlayerId,
                task is null ? "not the player's" : $"status {task.Status}"
            );

            return false;
        }

        var definition = _state.Definitions[task.DefinitionEntityId];

        if (!await GrantRewardAsync(task, definition, ct))
            return false;

        task.Status = DailyTaskStatus.Claimed;
        task.ClaimedAt = now;

        await SaveAsync([task], ct);
        await SendUpdatesAsync([task], now, ct);

        return true;
    }

    public async Task RecordActivityAsync(
        DailyTaskActivity activity,
        string value,
        CancellationToken ct
    )
    {
        if (string.IsNullOrEmpty(value))
            return;

        var now = Now;
        await EnsureDayAsync(now, ct);

        var changed = new List<PlayerDailyTaskEntity>();

        foreach (var task in _state.Tasks)
        {
            if (task.DayStartsAt != _state.DayStartsAt || task.Status != DailyTaskStatus.Active)
                continue;

            var definition = _state.Definitions[task.DefinitionEntityId];

            if (!Counts(task, definition, activity, value))
                continue;

            if (task.Repeats >= definition.RequiredRepeats)
            {
                task.Status = DailyTaskStatus.Completed;
                task.CompletedAt = now;
            }

            changed.Add(task);
        }

        if (changed.Count == 0)
            return;

        await SaveAsync(changed, ct);
        await SendUpdatesAsync(changed, now, ct);
        await AddBonusTaskAsync(now, ct);
    }

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    /// <summary>The start of the task day <paramref name="now"/> falls in.</summary>
    private DateTime DayStartsAt(DateTime now)
    {
        var start = now.Date + _config.ResetTimeUtc;

        return start > now ? start.AddDays(-1) : start;
    }

    private int SecondsLeft(PlayerDailyTaskEntity task, DateTime now) =>
        task.DayStartsAt == _state.DayStartsAt
            ? (int)Math.Ceiling((task.DayStartsAt.AddDays(1) - now).TotalSeconds)
            // The client lists an earlier day's completed task under "Unclaimed tasks" when its
            // time is below zero (AS3 DailyTaskInfo.isExpired).
            : -1;

    private ImmutableArray<DailyTaskSnapshot> Snapshots(
        IEnumerable<PlayerDailyTaskEntity> tasks,
        DateTime now
    ) =>
        [
            .. tasks.Select(x =>
                x.ToSnapshot(_state.Definitions[x.DefinitionEntityId], SecondsLeft(x, now))
            ),
        ];

    /// <summary>Whether the activity is a step of the task; adds it when it is.</summary>
    private static bool Counts(
        PlayerDailyTaskEntity task,
        DailyTaskDefinitionEntity definition,
        DailyTaskActivity activity,
        string value
    )
    {
        switch (definition.QuestType)
        {
            case DailyTaskTypes.EXPLORE when activity == DailyTaskActivity.RoomVisit:
            {
                var counted = task.Counted.Length == 0 ? [] : task.Counted.Split(',');

                if (counted.Contains(value))
                    return false;

                task.Counted = string.Join(',', [.. counted, value]);
                task.Repeats = counted.Length + 1;

                return true;
            }
            case DailyTaskTypes.FIND_FURNI when activity == DailyTaskActivity.FurniUse:
                if (!Targets(definition).Contains(value, StringComparer.OrdinalIgnoreCase))
                    return false;

                task.Repeats++;

                return true;
            default:
                return false;
        }
    }

    private static string[] Targets(DailyTaskDefinitionEntity definition) =>
        definition.Target.Split(
            ',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );

    /// <summary>
    /// Whether a definition can be given: a kind the server counts, a target for a find task,
    /// an explore count its counted rooms fit in, and a reward it can grant.
    /// </summary>
    private bool CanGive(DailyTaskDefinitionEntity definition)
    {
        var countable = definition.QuestType switch
        {
            // Each counted room takes its id and a comma.
            DailyTaskTypes.EXPLORE => definition.RequiredRepeats
                * (int.MaxValue.ToString(CultureInfo.InvariantCulture).Length + 1)
                <= PlayerDailyTaskEntity.COUNTED_MAX_LENGTH,
            DailyTaskTypes.FIND_FURNI => Targets(definition).Length > 0,
            _ => false,
        };
        var grantable =
            definition.RewardAmount == 0
            || definition.RewardProductType switch
            {
                ProductDisplayType.ActivityPoints => int.TryParse(
                    definition.RewardTypeId,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out _
                ),
                ProductDisplayType.Badge => definition.RewardTypeId.Length > 0,
                _ => false,
            };

        if (!countable || !grantable || definition.RequiredRepeats < 1)
        {
            _logger.LogWarning(
                "Daily task definition {DefinitionId} ({Code}) is enabled but cannot be given: type {QuestType}, {RequiredRepeats} repeats, reward {RewardProductType} {RewardTypeId}",
                definition.Id,
                definition.Code,
                definition.QuestType,
                definition.RequiredRepeats,
                definition.RewardProductType,
                definition.RewardTypeId
            );

            return false;
        }

        return true;
    }

    /// <summary>
    /// On the first call of a task day, reads the definitions and the player's tasks again (the
    /// day's, and earlier completed ones still claimable) and gives the day's tasks if the player
    /// has none yet.
    /// </summary>
    private async Task EnsureDayAsync(DateTime now, CancellationToken ct)
    {
        var day = DayStartsAt(now);

        if (_state.DayStartsAt == day)
            return;

        var keepFrom = day.AddDays(-_config.UnclaimedKeepDays);

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            var definitions = await dbCtx.DailyTaskDefinitions.AsNoTracking().ToListAsync(ct);
            var tasks = await dbCtx
                .PlayerDailyTasks.AsNoTracking()
                .Where(x =>
                    x.PlayerEntityId == _state.PlayerId.Value
                    && (
                        x.DayStartsAt == day
                        || (
                            x.Status == DailyTaskStatus.Completed
                            && x.DayStartsAt >= keepFrom
                            && x.DayStartsAt < day
                        )
                    )
                )
                .OrderBy(x => x.DayStartsAt)
                .ThenBy(x => x.Id)
                .ToListAsync(ct);

            _state.Definitions.Clear();

            foreach (var definition in definitions)
                _state.Definitions[definition.Id] = definition;

            _state.Tasks.Clear();
            _state.Tasks.AddRange(tasks);
        }

        _state.DayStartsAt = day;

        if (!_state.Tasks.Any(x => x.DayStartsAt == day))
            await GiveAsync(day, bonus: false, _config.TasksPerDay, ct);
    }

    /// <summary>Gives up to <paramref name="count"/> definitions the player has not had today, at random.</summary>
    private async Task<List<PlayerDailyTaskEntity>> GiveAsync(
        DateTime day,
        bool bonus,
        int count,
        CancellationToken ct
    )
    {
        var held = _state
            .Tasks.Where(x => x.DayStartsAt == day)
            .Select(x => x.DefinitionEntityId)
            .ToHashSet();
        var candidates = _state
            .Definitions.Values.Where(x =>
                x.Enabled && x.IsBonus == bonus && !held.Contains(x.Id) && CanGive(x)
            )
            .ToArray();

        Random.Shared.Shuffle(candidates);

        var given = candidates
            .Take(count)
            .Select(x => new PlayerDailyTaskEntity
            {
                PlayerEntityId = _state.PlayerId.Value,
                DefinitionEntityId = x.Id,
                DayStartsAt = day,
            })
            .ToList();

        if (given.Count == 0)
            return given;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        dbCtx.PlayerDailyTasks.AddRange(given);
        await dbCtx.SaveChangesAsync(ct);

        foreach (var task in given)
            dbCtx.Entry(task).State = EntityState.Detached;

        _state.Tasks.AddRange(given);

        return given;
    }

    /// <summary>
    /// Once every regular task of the day is completed, gives a bonus task if there is one and
    /// the player has none today: the client announces it with
    /// <c>dailytasks.bonus_available</c> ("All daily tasks completed, there's a special bonus task
    /// available!").
    /// </summary>
    private async Task AddBonusTaskAsync(DateTime now, CancellationToken ct)
    {
        var day = _state.DayStartsAt!.Value;
        var today = _state
            .Tasks.Where(x => x.DayStartsAt == day)
            .Select(x => (Task: x, _state.Definitions[x.DefinitionEntityId].IsBonus))
            .ToList();

        if (
            today.Any(x => x.IsBonus)
            || !today.Any(x => !x.IsBonus)
            || today.Any(x => !x.IsBonus && x.Task.Status == DailyTaskStatus.Active)
        )
            return;

        var given = await GiveAsync(day, bonus: true, count: 1, ct);

        if (given.Count == 0)
            return;

        await _grainFactory.SendComposerToPlayerAsync(
            _state.PlayerId,
            new DailyTasksTasksAddedMessageComposer { Tasks = Snapshots(given, now) },
            ct
        );
    }

    private async Task<bool> GrantRewardAsync(
        PlayerDailyTaskEntity task,
        DailyTaskDefinitionEntity definition,
        CancellationToken ct
    )
    {
        if (definition.RewardAmount == 0)
            return true;

        switch (definition.RewardProductType)
        {
            case ProductDisplayType.ActivityPoints
                when int.TryParse(
                    definition.RewardTypeId,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var pointType
                ):
            {
                var amount = definition.RewardAmount;

                if (
                    pointType == (int)ActivityPointType.Duckets
                    && await _grainFactory
                        .GetPlayerSubscriptionGrain(_state.PlayerId)
                        .HasActiveAsync(SubscriptionType.HabboClub, ct)
                )
                    amount *= _config.HcDucketMultiplier;

                var result = await _grainFactory
                    .GetPlayerWalletGrain(_state.PlayerId)
                    .CreditAsync(
                        CurrencyKind.ActivityPoints(pointType),
                        amount,
                        CREDIT_REFERENCE_PREFIX + task.Id.ToString(CultureInfo.InvariantCulture),
                        ct
                    );

                if (result is WalletCreditResult.Applied or WalletCreditResult.AlreadyApplied)
                    return true;

                _logger.LogError(
                    "Daily task {TaskId} of player {PlayerId}: crediting {Amount} of point type {PointType} failed with {Result}",
                    task.Id,
                    _state.PlayerId,
                    amount,
                    pointType,
                    result
                );

                return false;
            }
            case ProductDisplayType.Badge when definition.RewardTypeId.Length > 0:
                // Giving a badge the player already has changes nothing, so a retry is safe.
                await _grainFactory
                    .GetPlayerBadgeGrain(_state.PlayerId)
                    .GiveBadgeAsync(definition.RewardTypeId, ct);

                return true;
            default:
                _logger.LogError(
                    "Daily task {TaskId} of player {PlayerId}: reward {RewardProductType} {RewardTypeId} cannot be granted",
                    task.Id,
                    _state.PlayerId,
                    definition.RewardProductType,
                    definition.RewardTypeId
                );

                return false;
        }
    }

    private async Task SaveAsync(List<PlayerDailyTaskEntity> tasks, CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        dbCtx.PlayerDailyTasks.UpdateRange(tasks);
        await dbCtx.SaveChangesAsync(ct);

        foreach (var task in tasks)
            dbCtx.Entry(task).State = EntityState.Detached;
    }

    private Task SendUpdatesAsync(
        List<PlayerDailyTaskEntity> tasks,
        DateTime now,
        CancellationToken ct
    ) =>
        _grainFactory
            .GetPlayerPresenceGrain(_state.PlayerId)
            .SendComposerAsync(
                [
                    .. tasks.Select(x => new DailyTasksTaskUpdateMessageComposer
                    {
                        TaskId = x.Id,
                        Repeats = x.Repeats,
                        Status = x.Status,
                        SecondsLeft = SecondsLeft(x, now),
                    }),
                ],
                ct
            );
}
