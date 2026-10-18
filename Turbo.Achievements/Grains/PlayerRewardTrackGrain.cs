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
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Pets.Providers;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Quests.Enums;
using Turbo.Primitives.Quests.Grains;
using Turbo.Primitives.Quests.RewardTracks;
using Turbo.Primitives.Quests.Snapshots;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Achievements.Grains;

/// <summary>
/// A player's reward track progress, write-through: each step, claim and purchase is saved
/// before it is sent. The tracks are the hotel's (<see cref="IRewardTrackStore"/>, the live ones at
/// each step); the grain keeps one row per track the player has progress on. How the client reads it is the AS3
/// RewardTrack data classes: a task's count crossing a level's count gives that level's points
/// (a premium level only to a premium owner, every level's points times the premium boost for
/// one), and a prize can be claimed when it is not premium-locked and the points reach it.
/// </summary>
internal sealed class PlayerRewardTrackGrain : Grain, IPlayerRewardTrackGrain
{
    private const string CREDIT_REFERENCE_PREFIX = "rewardtrack:";
    private const string GENERIC_BOT_TYPE = "GENERIC";

    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly RewardTrackConfig _config;
    private readonly IRewardTrackStore _tracks;
    private readonly IGrainFactory _grainFactory;
    private readonly IFurnitureDefinitionProvider _definitions;
    private readonly IPetBreedProvider _petBreeds;
    private readonly ILogger<IPlayerRewardTrackGrain> _logger;

    private readonly PlayerRewardTrackLiveState _state;

    public PlayerRewardTrackGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<RewardTrackConfig> config,
        IRewardTrackStore tracks,
        IGrainFactory grainFactory,
        IFurnitureDefinitionProvider definitions,
        IPetBreedProvider petBreeds,
        ILogger<IPlayerRewardTrackGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _config = config.Value;
        _tracks = tracks;
        _grainFactory = grainFactory;
        _definitions = definitions;
        _petBreeds = petBreeds;
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

    public Task SendTracksAsync(CancellationToken ct) => SendTracksAsync(reload: false, ct);

    // RewardTrackController.onRewardTracks: a reload rebuilds the window's views and, when it is
    // open, says so ("reward_track.reload.*").
    public Task ReloadTracksAsync(CancellationToken ct) => SendTracksAsync(reload: true, ct);

    private Task SendTracksAsync(bool reload, CancellationToken ct) =>
        _grainFactory.SendComposerToPlayerAsync(
            _state.PlayerId,
            new RewardTracksMessageComposer
            {
                Disabled = !_config.Enabled,
                Tracks = _config.Enabled ? [.. Live().Select(Snapshot)] : [],
                Reload = reload,
            },
            ct
        );

    /// <summary>The tracks players get now; one that ended no longer counts or gives.</summary>
    private ImmutableArray<RewardTrackDefinition> Live() => _tracks.LiveAt(DateTime.UtcNow);

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

        foreach (var track in Live())
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

                changed.Add(track.Id);
                updates.Add(Advance(track, task, count + 1));
            }
        }

        if (changed.Count == 0)
            return;

        await SaveAsync(changed, ct);
        await _grainFactory.GetPlayerPresenceGrain(_state.PlayerId).SendComposerAsync(updates, ct);
    }

    public async Task<bool> ProgressTaskAsync(
        string trackId,
        string taskId,
        int score,
        bool add,
        CancellationToken ct
    )
    {
        if (
            !_config.Enabled
            || Live().FirstOrDefault(x => x.Id == trackId) is not { } track
            || track.Tasks.Find(x => x.Id == taskId) is not { } task
            || (task.Premium && !IsPremium(trackId))
        )
            return false;

        var count = ProgressOf(trackId).Tasks.GetValueOrDefault(taskId);
        var target = Math.Min(
            add ? (long)count + Math.Max(0, score) : score,
            task.Levels[^1].RequiredCount
        );

        if (target <= count)
            return false;

        var update = Advance(track, task, (int)target);

        await SaveAsync([trackId], ct);
        await _grainFactory.SendComposerToPlayerAsync(_state.PlayerId, update, ct);

        return true;
    }

    public async Task<bool> ResetTrackAsync(string trackId, CancellationToken ct)
    {
        if (
            !_config.Enabled
            || !Live().Any(x => x.Id == trackId)
            || !_state.Rows.TryGetValue(trackId, out var row)
        )
            return false;

        row.Points = 0;
        _state.Progress[trackId] = new();

        await SaveAsync([trackId], ct);
        await ReloadTracksAsync(ct);

        return true;
    }

    /// <summary>
    /// Moves a task's count up to <paramref name="target"/>, adding the points of each level it
    /// passes (a premium level only for a premium owner, boosted for one).
    /// </summary>
    private RewardTrackProgressMessageComposer Advance(
        RewardTrackDefinition track,
        RewardTrackTaskDefinition task,
        int target
    )
    {
        var premium = IsPremium(track.Id);
        var progress = ProgressOf(track.Id);
        var count = progress.Tasks.GetValueOrDefault(task.Id);
        var row = RowOf(track.Id);

        progress.Tasks[task.Id] = target;
        row.Points += task
            .Levels.Where(x =>
                x.RequiredCount > count && x.RequiredCount <= target && (!x.Premium || premium)
            )
            .Sum(x => Boosted(track, x.Points, premium));

        return new RewardTrackProgressMessageComposer
        {
            TrackId = track.Id,
            TaskId = task.Id,
            ProgressCount = target,
            Points = row.Points,
        };
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

        var track = Live().FirstOrDefault(x => x.Id == trackId);

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

        var track = Live().FirstOrDefault(x => x.Id == trackId);

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
                .TryDebitAsync(
                    costs,
                    MovementCause.New(
                        MovementCauses.REWARD_TRACK,
                        _state.PlayerId.Value,
                        $"{track.Id}:premium"
                    ),
                    ct
                );

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

    /// <summary>
    /// The furniture a furni prize gives: the definition it names, else the hotel's furni drawn
    /// by its sprite (<see cref="RewardTrackPrizeDefinition.RewardTypeId"/>), as Habbo's prizes
    /// name them.
    /// </summary>
    private int? FurnitureOf(RewardTrackPrizeDefinition prize)
    {
        if (prize.FurnitureDefinitionId is { } definitionId)
            return definitionId;

        if (
            !int.TryParse(
                prize.RewardTypeId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var spriteId
            )
        )
            return null;

        return _definitions
            .TryGetDefinitionBySprite(
                prize.ProductType == ProductDisplayType.WallItem
                    ? ProductType.Wall
                    : ProductType.Floor,
                spriteId
            )
            ?.Id;
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
                        MovementCause.New(
                            MovementCauses.REWARD_TRACK,
                            _state.PlayerId.Value,
                            $"{track.Id}:{prize.Id}"
                        ),
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
            or ProductDisplayType.WallItem when FurnitureOf(prize) is { } definitionId:
            {
                var inventory = _grainFactory.GetInventoryGrain(_state.PlayerId);

                return await GiveEachAsync(
                    track,
                    prize,
                    async () =>
                    {
                        var cause = MovementCause.New(
                            MovementCauses.REWARD_TRACK,
                            _state.PlayerId.Value,
                            $"{track.Id}:{prize.Id}"
                        );

                        if (
                            await inventory.GrantFurnitureAsync(definitionId, null, cause, ct)
                            is not null
                        )
                            return true;

                        _logger.LogError(
                            "Reward track prize {TrackId}/{PrizeId}: furniture definition {DefinitionId} does not exist",
                            track.Id,
                            prize.Id,
                            definitionId
                        );

                        return false;
                    },
                    ct
                );
            }
            case ProductDisplayType.Habbicon
                when int.TryParse(
                    prize.RewardTypeId,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var habbiconId
                ):
                // Giving a Habbicon the player already has changes nothing, so a retry is safe.
                return await _grainFactory
                    .GetPlayerHabbiconGrain(_state.PlayerId)
                    .GiveAsync(habbiconId, ct);
            case ProductDisplayType.Pet when PetOf(prize) is { } pet:
            {
                var inventory = _grainFactory.GetInventoryGrain(_state.PlayerId);
                var name = prize.Name.Length > 0 ? prize.Name : _config.PetPrizeName;

                return await GiveEachAsync(
                    track,
                    prize,
                    async () =>
                        await inventory.CreatePetAsync(
                            name,
                            pet.TypeId,
                            pet.PaletteId,
                            pet.BreedId,
                            pet.Color,
                            pet.RarityLevel,
                            [],
                            MovementCause.New(
                                MovementCauses.REWARD_TRACK,
                                _state.PlayerId.Value,
                                $"{track.Id}:{prize.Id}"
                            ),
                            ct
                        )
                            is not null,
                    ct
                );
            }
            // The client draws a bot prize by its look alone (ProductIconWidget); "GENERIC" is
            // the only bot type Habbo's prizes name, and the only one a bot can be created as here.
            case ProductDisplayType.Bot
                when prize.ExtraParams.Length > 0
                    && (
                        prize.RewardTypeId.Length == 0
                        || string.Equals(
                            prize.RewardTypeId,
                            GENERIC_BOT_TYPE,
                            StringComparison.OrdinalIgnoreCase
                        )
                    ):
            {
                var inventory = _grainFactory.GetInventoryGrain(_state.PlayerId);
                var name = prize.Name.Length > 0 ? prize.Name : _config.BotPrizeName;

                return await GiveEachAsync(
                    track,
                    prize,
                    async () =>
                        await inventory.CreateBotAsync(
                            name,
                            string.Empty,
                            prize.ExtraParams,
                            AvatarGenderType.Male,
                            MovementCause.New(
                                MovementCauses.REWARD_TRACK,
                                _state.PlayerId.Value,
                                $"{track.Id}:{prize.Id}"
                            ),
                            ct
                        )
                            is not null,
                    ct
                );
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

    /// <summary>
    /// Gives a prize's items one at a time. Unlike a credit or a badge, a second grant is a second
    /// item, so each one given is saved before the next: a claim that failed part way gives only
    /// the rest on retry.
    /// </summary>
    private async Task<bool> GiveEachAsync(
        RewardTrackDefinition track,
        RewardTrackPrizeDefinition prize,
        Func<Task<bool>> giveOne,
        CancellationToken ct
    )
    {
        var progress = ProgressOf(track.Id);

        RowOf(track.Id);

        for (
            var given = progress.FurnitureGiven.GetValueOrDefault(prize.Id);
            given < prize.Amount;
            given++
        )
        {
            if (!await giveOne())
                return false;

            progress.FurnitureGiven[prize.Id] = given + 1;
            await SaveAsync([track.Id], ct);
        }

        return true;
    }

    /// <summary>
    /// The pet a pet prize gives: its figure (<see cref="RewardTrackPrizeDefinition.ExtraParams"/>,
    /// "type palette colour" as Habbo's prizes send it, e.g. "1 0 ABABAB") with the breed and
    /// rarity of the hotel's palette. Null when the hotel has no such palette.
    /// </summary>
    private PetPrize? PetOf(RewardTrackPrizeDefinition prize)
    {
        var parts = prize.ExtraParams.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (
            parts.Length < 3
            || !int.TryParse(
                parts[0],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var typeId
            )
            || !int.TryParse(
                parts[1],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var paletteId
            )
            || _petBreeds.TryGetPalette(typeId, paletteId) is not { } palette
        )
            return null;

        return new(typeId, paletteId, palette.BreedId, parts[2], palette.RarityLevel);
    }

    private sealed record PetPrize(
        int TypeId,
        int PaletteId,
        int BreedId,
        string Color,
        int RarityLevel
    );

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
