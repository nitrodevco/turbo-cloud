using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Logging;
using Turbo.Players.Configuration;
using Turbo.Primitives;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Players.Grains;

/// <summary>
/// A player's profile. Write-through: every change is saved as it happens, and deactivation
/// saves once more so whatever changed last is not lost.
/// </summary>
internal sealed class PlayerGrain : Grain, IPlayerGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly PlayerConfig _playerConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IPlayerGrain> _logger;

    private readonly PlayerLiveState _state;

    public PlayerId PlayerId => _state.PlayerId;

    public PlayerGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<PlayerConfig> playerConfig,
        IGrainFactory grainFactory,
        ILogger<IPlayerGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _playerConfig = playerConfig.Value;
        _grainFactory = grainFactory;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hydrate player {PlayerId}", _state.PlayerId);

            throw;
        }
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        // Nothing can act on a failure here, but losing the last changes must not be silent.
        try
        {
            await WriteToDatabaseAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to save player {PlayerId} on deactivation",
                _state.PlayerId
            );
        }
    }

    public async Task SetOnlineStatusAsync(bool flag, CancellationToken ct)
    {
        _state.IsOnline = flag;

        var playerPresence = _grainFactory.GetPlayerPresenceGrain(PlayerId);

        await playerPresence.OnPlayerUpdatedAsync(await GetSummaryAsync(ct), ct);
    }

    public async Task SetFigureAsync(string figure, AvatarGenderType gender, CancellationToken ct)
    {
        _state.Figure = figure;
        _state.Gender = gender;

        await WriteToDatabaseAsync(ct);

        var playerPresence = _grainFactory.GetPlayerPresenceGrain(PlayerId);

        await playerPresence.OnFigureUpdatedAsync(await GetSummaryAsync(ct), ct);
    }

    public async Task SetMottoAsync(string text, CancellationToken ct)
    {
        _state.Motto = text;

        await WriteToDatabaseAsync(ct);

        var playerPresence = _grainFactory.GetPlayerPresenceGrain(PlayerId);

        await playerPresence.OnPlayerUpdatedAsync(await GetSummaryAsync(ct), ct);
    }

    private async Task HydrateAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entity =
            await dbCtx
                .Players.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == (int)_state.PlayerId, ct)
            ?? throw new TurboException(TurboErrorCodeEnum.PlayerNotFound);

        _state.Name = entity.Name;
        _state.Motto = entity.Motto ?? string.Empty;
        _state.Figure = entity.Figure;
        _state.Gender = entity.Gender;
        _state.AchievementScore = 0;
        _state.CreatedAt = entity.CreatedAt;
        _state.LastUpdated = entity.UpdatedAt;
        _state.Perks = entity.PlayerPerks;
        _state.RespectPoints = entity.RespectPoints;
        _state.RespectsLeft = entity.RespectsLeft;
        _state.PetRespectsLeft = entity.PetRespectsLeft;
        _state.RespectReplenishesLeft = entity.RespectReplenishesLeft;
        _state.RespectResetDate = entity.RespectResetDate;

        ResetDailyRespectIfDue();

        await _grainFactory.GetPlayerDirectoryGrain().SetPlayerNameAsync(PlayerId, _state.Name, ct);
    }

    private async Task WriteToDatabaseAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var snapshot = await GetSummaryAsync(ct);

        await dbCtx
            .Players.Where(x => x.Id == (int)_state.PlayerId)
            .ExecuteUpdateAsync(
                up =>
                    up.SetProperty(p => p.Name, snapshot.Name)
                        .SetProperty(p => p.Motto, snapshot.Motto)
                        .SetProperty(p => p.Figure, snapshot.Figure)
                        .SetProperty(p => p.Gender, snapshot.Gender)
                        .SetProperty(p => p.RespectPoints, _state.RespectPoints)
                        .SetProperty(p => p.RespectsLeft, _state.RespectsLeft)
                        .SetProperty(p => p.PetRespectsLeft, _state.PetRespectsLeft)
                        .SetProperty(p => p.RespectReplenishesLeft, _state.RespectReplenishesLeft)
                        .SetProperty(p => p.RespectResetDate, _state.RespectResetDate),
                ct
            );

        _state.LastUpdated = DateTime.Now;
    }

    public Task<PlayerSummarySnapshot> GetSummaryAsync(CancellationToken ct) =>
        Task.FromResult(
            new PlayerSummarySnapshot
            {
                PlayerId = _state.PlayerId,
                Name = _state.Name,
                Motto = _state.Motto,
                Figure = _state.Figure,
                Gender = _state.Gender,
                AchievementScore = _state.AchievementScore,
                BadgesRank = _state.BadgesRank,
                IsOnline = _state.IsOnline,
                CreatedAt = _state.CreatedAt,
                LastUpdated = _state.LastUpdated,
                RespectPoints = _state.RespectPoints,
                RespectsLeft = _state.RespectsLeft,
                PetRespectsLeft = _state.PetRespectsLeft,
                RespectReplenishesLeft = _state.RespectReplenishesLeft,
                Perks = _state.Perks,
            }
        );

    public async Task<bool> TryUseRespectAsync(CancellationToken ct)
    {
        ResetDailyRespectIfDue();

        if (_state.RespectsLeft <= 0)
            return false;

        _state.RespectsLeft--;

        await WriteToDatabaseAsync(ct);

        return true;
    }

    public async Task<bool> TryUsePetRespectAsync(CancellationToken ct)
    {
        ResetDailyRespectIfDue();

        if (_state.PetRespectsLeft <= 0)
            return false;

        _state.PetRespectsLeft--;

        await WriteToDatabaseAsync(ct);

        return true;
    }

    public async Task<int> ReceiveRespectAsync(CancellationToken ct)
    {
        _state.RespectPoints++;

        await WriteToDatabaseAsync(ct);

        return _state.RespectPoints;
    }

    public async Task<bool> ReplenishRespectAsync(CancellationToken ct)
    {
        ResetDailyRespectIfDue();

        if (_state.RespectReplenishesLeft <= 0)
            return false;

        _state.RespectReplenishesLeft--;
        _state.RespectsLeft = _playerConfig.MaxRespectPerDay;

        await WriteToDatabaseAsync(ct);

        return true;
    }

    /// <summary>
    /// Daily respect allowances refill at UTC midnight. Checked lazily on every use, so a grain
    /// that stays active across midnight still resets.
    /// </summary>
    private void ResetDailyRespectIfDue()
    {
        var today = DateTime.UtcNow.Date;

        if (_state.RespectResetDate == today)
            return;

        _state.RespectResetDate = today;
        _state.RespectsLeft = _playerConfig.MaxRespectPerDay;
        _state.PetRespectsLeft = _playerConfig.MaxPetRespectPerDay;
        _state.RespectReplenishesLeft = _playerConfig.RespectReplenishesPerDay;
    }

    public async Task SetBadgesRankAsync(int badgesRank, CancellationToken ct)
    {
        if (_state.BadgesRank == badgesRank)
            return;

        _state.BadgesRank = badgesRank;

        // Only the room shows a rank, so friends are not told as they are of a new figure.
        await _grainFactory
            .GetPlayerPresenceGrain(PlayerId)
            .OnBadgesRankChangedAsync(await GetSummaryAsync(ct), ct);
    }

    // The badge figures of a profile are not here: they are the inventory's, and this grain must
    // not await the inventory (inventory -> presence -> this grain is already a chain). The
    // handler reads both and sends them side by side.
    public Task<PlayerExtendedProfileSnapshot> GetExtendedProfileSnapshotAsync(
        CancellationToken ct
    ) =>
        Task.FromResult(
            new PlayerExtendedProfileSnapshot
            {
                UserId = _state.PlayerId,
                UserName = _state.Name,
                Figure = _state.Figure,
                Motto = _state.Motto,
                CreationDate = _state.CreatedAt.ToString("yyyy-MM-dd"),
                AchievementScore = _state.AchievementScore,
                FriendCount = 0,
                IsFriend = false,
                IsFriendRequestSent = false,
                IsOnline = _state.IsOnline,
                Guilds = [],
                LastAccessSinceInSeconds = 0,
                OpenProfileWindow = true,
                IsHidden = false,
                AccountLevel = 1,
                IntegerField24 = 0,
                StarGemCount = 0,
                BooleanField26 = false,
                BooleanField27 = false,
                AchievementLevel = 0,
            }
        );
}
