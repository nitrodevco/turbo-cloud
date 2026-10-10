using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Achievements.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Quests;
using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Quests.Enums;
using Turbo.Primitives.Quests.Grains;
using Turbo.Primitives.Quests.Snapshots;

namespace Turbo.Achievements.Grains;

/// <summary>
/// A player's reward track progress, write-through: each step, claim and purchase is saved
/// before it is sent. The tracks are configuration (<see cref="RewardTrackConfig"/>); the grain
/// keeps one row per track the player has progress on. How the client reads it is the AS3
/// RewardTrack data classes: a task's count crossing a level's count gives that level's points
/// (a premium level only to a premium owner, every level's points times the premium boost for
/// one), and a prize can be claimed when it is not premium-locked and the points reach it.
/// </summary>
internal sealed class PlayerRewardTrackGrain : Grain, IPlayerRewardTrackGrain
{
    private const string CREDIT_REFERENCE_PREFIX = "rewardtrack:";

    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly RewardTrackConfig _config;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IPlayerRewardTrackGrain> _logger;

    private readonly PlayerRewardTrackLiveState _state;

    public PlayerRewardTrackGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<RewardTrackConfig> config,
        IGrainFactory grainFactory,
        ILogger<IPlayerRewardTrackGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _config = config.Value;
        _grainFactory = grainFactory;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var rows = await dbCtx
                .PlayerRewardTracks.AsNoTracking()
                .Where(x => x.PlayerEntityId == _state.PlayerId.Value)
                .ToListAsync(ct);

            foreach (var row in rows)
            {
                _state.Rows[row.TrackId] = row;
                _state.Progress[row.TrackId] =
                    JsonSerializer.Deserialize<RewardTrackProgress>(row.ProgressJson) ?? new();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to hydrate reward tracks for player {PlayerId}",
                _state.PlayerId
            );

            throw;
        }
    }

    public Task SendTracksAsync(CancellationToken ct) =>
        _grainFactory.SendComposerToPlayerAsync(
            _state.PlayerId,
            new RewardTracksMessageComposer
            {
                Disabled = !_config.Enabled,
                Tracks = _config.Enabled ? [.. _config.Tracks.Select(Snapshot)] : [],
                Reload = false,
            },
            ct
        );

    public async Task RecordActionAsync(
        string actionType,
        string distinctValue,
        CancellationToken ct
    )
    {
        if (!_config.Enabled)
            return;

        var updates = new List<IComposer>();
        var changed = new HashSet<string>();

        foreach (var track in _config.Tracks)
        {
            foreach (var task in track.Tasks)
            {
                if (!string.Equals(task.ActionType, actionType, StringComparison.OrdinalIgnoreCase))
                    continue;

                var premium = IsPremium(track.Id);

                if (task.Premium && !premium)
                    continue;

                var progress = ProgressOf(track.Id);
                var count = progress.Tasks.GetValueOrDefault(task.Id);

                // A task stops counting at its last level, where the client shows it complete.
                if (count >= task.Levels[^1].RequiredCount)
                    continue;

                if (distinctValue.Length > 0)
                {
                    if (!progress.Counted.TryGetValue(task.Id, out var counted))
                        progress.Counted[task.Id] = counted = [];

                    if (counted.Contains(distinctValue))
                        continue;

                    counted.Add(distinctValue);
                }

                var row = RowOf(track.Id);

                progress.Tasks[task.Id] = count + 1;
                row.Points += task
                    .Levels.Where(x =>
                        x.RequiredCount > count
                        && x.RequiredCount <= count + 1
                        && (!x.Premium || premium)
                    )
                    .Sum(x => Boosted(track, x.Points, premium));

                changed.Add(track.Id);
                updates.Add(
                    new RewardTrackProgressMessageComposer
                    {
                        TrackId = track.Id,
                        TaskId = task.Id,
                        ProgressCount = count + 1,
                        Points = row.Points,
                    }
                );
            }
        }

        if (changed.Count == 0)
            return;

        await SaveAsync(changed, ct);
        await _grainFactory.GetPlayerPresenceGrain(_state.PlayerId).SendComposerAsync(updates, ct);
    }

    public async Task ClaimPrizeAsync(string trackId, string prizeId, CancellationToken ct)
    {
        var result = await ClaimAsync(trackId, prizeId, ct);

        if (result != RewardTrackClaimResult.Success)
            _logger.LogWarning(
                "Rejected reward track prize {TrackId}/{PrizeId} for player {PlayerId}: {Result}",
                trackId,
                prizeId,
                _state.PlayerId,
                result
            );

        await _grainFactory.SendComposerToPlayerAsync(
            _state.PlayerId,
            new RewardTrackClaimResultMessageComposer
            {
                TrackId = trackId,
                PrizeId = prizeId,
                Result = result,
            },
            ct
        );
    }

    public async Task PurchasePremiumAsync(string trackId, CancellationToken ct)
    {
        var result = await BuyPremiumAsync(trackId, ct);

        if (result != RewardTrackPremiumResult.Success)
            _logger.LogWarning(
                "Rejected reward track premium {TrackId} for player {PlayerId}: {Result}",
                trackId,
                _state.PlayerId,
                result
            );

        await _grainFactory.SendComposerToPlayerAsync(
            _state.PlayerId,
            new RewardTrackPremiumPurchaseResultMessageComposer
            {
                TrackId = trackId,
                Result = result,
                Points = _state.Rows.TryGetValue(trackId, out var row) ? row.Points : 0,
            },
            ct
        );
    }

    private async Task<RewardTrackClaimResult> ClaimAsync(
        string trackId,
        string prizeId,
        CancellationToken ct
    )
    {
        if (!_config.Enabled)
            return RewardTrackClaimResult.Disabled;

        var track = _config.Tracks.Find(x => x.Id == trackId);

        if (track is null)
            return RewardTrackClaimResult.TrackNotFound;

        var prize = track.Prizes.Find(x => x.Id == prizeId);

        if (prize is null)
            return RewardTrackClaimResult.PrizeNotFound;

        var progress = ProgressOf(trackId);

        if (progress.Claimed.Contains(prizeId))
            return RewardTrackClaimResult.AlreadyClaimed;

        if (prize.Premium && !IsPremium(trackId))
            return RewardTrackClaimResult.PremiumRequired;

        if (PointsOf(trackId) < prize.RequiredPoints)
            return RewardTrackClaimResult.NotEnoughPoints;

        if (!await GrantAsync(track, prize, ct))
            return RewardTrackClaimResult.Failed;

        RowOf(trackId);
        progress.Claimed.Add(prizeId);
        progress.FurnitureGiven.Remove(prizeId);
        await SaveAsync([trackId], ct);

        return RewardTrackClaimResult.Success;
    }

    private async Task<RewardTrackPremiumResult> BuyPremiumAsync(
        string trackId,
        CancellationToken ct
    )
    {
        if (!_config.Enabled)
            return RewardTrackPremiumResult.Disabled;

        var track = _config.Tracks.Find(x => x.Id == trackId);

        if (track is null)
            return RewardTrackPremiumResult.TrackNotFound;

        if (track.Premium is not { } premium)
            return RewardTrackPremiumResult.NotConfigured;

        if (IsPremium(trackId))
            return RewardTrackPremiumResult.AlreadyOwned;

        var costs = new List<WalletDebitRequest>();

        if (premium.CostCredits > 0)
            costs.Add(
                new WalletDebitRequest
                {
                    CurrencyKind = CurrencyKind.Credits,
                    Amount = premium.CostCredits,
                }
            );

        if (premium.CostDiamonds > 0)
            costs.Add(
                new WalletDebitRequest
                {
                    CurrencyKind = CurrencyKind.ActivityPoints((int)ActivityPointType.Diamonds),
                    Amount = premium.CostDiamonds,
                }
            );

        if (costs.Count > 0)
        {
            var debit = await _grainFactory
                .GetPlayerWalletGrain(_state.PlayerId)
                .TryDebitAsync(costs, ct);

            if (!debit.Succeeded)
                return debit.Failure?.CurrencyKind.CurrencyType switch
                {
                    CurrencyType.Credits => RewardTrackPremiumResult.NotEnoughCredits,
                    CurrencyType.ActivityPoints => RewardTrackPremiumResult.NotEnoughDiamonds,
                    _ => RewardTrackPremiumResult.Failed,
                };
        }

        var row = RowOf(trackId);
        var progress = ProgressOf(trackId);

        row.Premium = true;
        // The instant points, and the premium levels the player has already passed, which
        // counted for nothing until now.
        row.Points +=
            premium.InstantPoints
            + track
                .Tasks.SelectMany(task =>
                    task.Levels.Where(level =>
                        level.Premium
                        && progress.Tasks.GetValueOrDefault(task.Id) >= level.RequiredCount
                    )
                )
                .Sum(level => Boosted(track, level.Points, premium: true));

        await SaveAsync([trackId], ct);

        return RewardTrackPremiumResult.Success;
    }

    private async Task<bool> GrantAsync(
        RewardTrackDefinition track,
        RewardTrackPrizeDefinition prize,
        CancellationToken ct
    )
    {
        switch (prize.ProductType)
        {
            case ProductDisplayType.ActivityPoints
                when int.TryParse(
                    prize.RewardTypeId,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var pointType
                ):
            {
                var result = await _grainFactory
                    .GetPlayerWalletGrain(_state.PlayerId)
                    .CreditAsync(
                        CurrencyKind.ActivityPoints(pointType),
                        prize.Amount,
                        $"{CREDIT_REFERENCE_PREFIX}{track.Id}:{prize.Id}",
                        ct
                    );

                return result is WalletCreditResult.Applied or WalletCreditResult.AlreadyApplied;
            }
            case ProductDisplayType.Badge when prize.RewardTypeId.Length > 0:
                // Giving a badge the player already has changes nothing, so a retry is safe.
                await _grainFactory
                    .GetPlayerBadgeGrain(_state.PlayerId)
                    .GiveBadgeAsync(prize.RewardTypeId, ct);

                return true;
            case ProductDisplayType.FloorItem
            or ProductDisplayType.WallItem when prize.FurnitureDefinitionId is { } definitionId:
            {
                // Unlike a credit or a badge, a second grant is a second item, so each one given is
                // saved before the next: a claim that failed part way gives only the rest on retry.
                var inventory = _grainFactory.GetInventoryGrain(_state.PlayerId);
                var progress = ProgressOf(track.Id);

                RowOf(track.Id);

                for (
                    var given = progress.FurnitureGiven.GetValueOrDefault(prize.Id);
                    given < prize.Amount;
                    given++
                )
                {
                    if (await inventory.GrantFurnitureAsync(definitionId, null, ct) is null)
                    {
                        _logger.LogError(
                            "Reward track prize {TrackId}/{PrizeId}: furniture definition {DefinitionId} does not exist",
                            track.Id,
                            prize.Id,
                            definitionId
                        );

                        return false;
                    }

                    progress.FurnitureGiven[prize.Id] = given + 1;
                    await SaveAsync([track.Id], ct);
                }

                return true;
            }
            default:
                _logger.LogError(
                    "Reward track prize {TrackId}/{PrizeId} cannot be granted: {ProductType} {RewardTypeId}",
                    track.Id,
                    prize.Id,
                    prize.ProductType,
                    prize.RewardTypeId
                );

                return false;
        }
    }

    private static int Boosted(RewardTrackDefinition track, int points, bool premium) =>
        premium && track.Premium is { } config
            ? (int)Math.Round(points * config.TaskPointsBoost, MidpointRounding.AwayFromZero)
            : points;

    private bool IsPremium(string trackId) =>
        _state.Rows.TryGetValue(trackId, out var row) && row.Premium;

    private int PointsOf(string trackId) =>
        _state.Rows.TryGetValue(trackId, out var row) ? row.Points : 0;

    private RewardTrackProgress ProgressOf(string trackId)
    {
        if (!_state.Progress.TryGetValue(trackId, out var progress))
            _state.Progress[trackId] = progress = new();

        return progress;
    }

    private PlayerRewardTrackEntity RowOf(string trackId)
    {
        if (!_state.Rows.TryGetValue(trackId, out var row))
            _state.Rows[trackId] = row = new()
            {
                PlayerEntityId = _state.PlayerId.Value,
                TrackId = trackId,
            };

        return row;
    }

    private async Task SaveAsync(IEnumerable<string> trackIds, CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var rows = trackIds.Select(id => _state.Rows[id]).ToList();

        foreach (var row in rows)
        {
            row.ProgressJson = JsonSerializer.Serialize(_state.Progress[row.TrackId]);

            if (row.Id == 0)
                dbCtx.PlayerRewardTracks.Add(row);
            else
                dbCtx.PlayerRewardTracks.Update(row);
        }

        await dbCtx.SaveChangesAsync(ct);

        foreach (var row in rows)
            dbCtx.Entry(row).State = EntityState.Detached;
    }

    private RewardTrackSnapshot Snapshot(RewardTrackDefinition track)
    {
        var premium = IsPremium(track.Id);
        var points = PointsOf(track.Id);
        var progress = _state.Progress.GetValueOrDefault(track.Id) ?? new();
        var prizes = track
            .Prizes.Select(x => new RewardTrackPrizeSnapshot
            {
                Id = x.Id,
                RequiredPoints = x.RequiredPoints,
                ProductType = x.ProductType,
                RewardTypeId = x.RewardTypeId,
                ExtraParams = x.ExtraParams,
                Amount = x.Amount,
                Premium = x.Premium,
                Available = (!x.Premium || premium) && points >= x.RequiredPoints,
                Claimed = progress.Claimed.Contains(x.Id),
            })
            .ToImmutableArray();
        var complete = prizes.Where(x => !x.Premium).All(x => x.Claimed);

        return new()
        {
            Id = track.Id,
            Theme = track.Theme,
            Points = points,
            PremiumConfig = track.Premium is { } config
                ? new()
                {
                    TaskPointsBoost = config.TaskPointsBoost,
                    InstantPoints = config.InstantPoints,
                    CostDiamonds = config.CostDiamonds,
                    CostCredits = config.CostCredits,
                }
                : null,
            Premium = premium,
            Complete = complete,
            // As the client's RewardTrack.refreshDerivedState works it out.
            PremiumComplete = track.Premium is null || (complete && prizes.All(x => x.Claimed)),
            Tasks =
            [
                .. track.Tasks.Select(x => new RewardTrackTaskSnapshot
                {
                    Id = x.Id,
                    ActionType = x.ActionType,
                    Parameter = x.Parameter,
                    ProgressCount = progress.Tasks.GetValueOrDefault(x.Id),
                    Premium = x.Premium,
                    Levels =
                    [
                        .. x.Levels.Select(level => new RewardTrackTaskLevelSnapshot
                        {
                            RequiredCount = level.RequiredCount,
                            PointsReward = level.Points,
                            Premium = level.Premium,
                        }),
                    ],
                }),
            ],
            Prizes = prizes,
        };
    }
}
