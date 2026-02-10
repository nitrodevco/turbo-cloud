using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Orleans;
using Turbo.Database.Context;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Grains.Players;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Players.Grains;

internal sealed class PlayerGrain : Grain, IPlayerGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly IGrainFactory _grainFactory;
    private readonly IConfiguration _configuration;

    private readonly PlayerLiveState _state;

    public PlayerGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IGrainFactory grainFactory,
        IConfiguration configuration
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _grainFactory = grainFactory;
        _configuration = configuration;

        _state = new() { PlayerId = PlayerId.Parse((int)this.GetPrimaryKeyLong()) };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        await HydrateAsync(ct);
        await TryResetDailyRespectIfNeededAsync(ct);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        await WriteToDatabaseAsync(ct);
    }

    public async Task SetFigureAsync(string figure, AvatarGenderType gender, CancellationToken ct)
    {
        _state.Figure = figure;
        _state.Gender = gender;

        await WriteToDatabaseAsync(ct);

        var playerPresence = _grainFactory.GetPlayerPresenceGrain((int)this.GetPrimaryKeyLong());

        await playerPresence.OnFigureUpdatedAsync(await GetSummaryAsync(ct), ct);

        await WriteToDatabaseAsync(ct);
    }

    public async Task SetMottoAsync(string text, CancellationToken ct)
    {
        _state.Motto = text;

        await WriteToDatabaseAsync(ct);

        var playerPresence = _grainFactory.GetPlayerPresenceGrain((int)this.GetPrimaryKeyLong());

        await playerPresence.OnPlayerUpdatedAsync(await GetSummaryAsync(ct), ct);

        await WriteToDatabaseAsync(ct);
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
        _state.RespectTotal = entity.RespectTotal;
        _state.RespectLeft = entity.RespectLeft;
        _state.PetRespectLeft = entity.PetRespectLeft;
        _state.RespectReplenishesLeft = entity.RespectReplenishesLeft;
        _state.LastRespectReset = entity.LastRespectReset ?? DateTime.MinValue;

        await _grainFactory
            .GetPlayerDirectoryGrain()
            .SetPlayerNameAsync(PlayerId.Parse((int)this.GetPrimaryKeyLong()), _state.Name, ct);
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
                        .SetProperty(p => p.RespectTotal, _state.RespectTotal)
                        .SetProperty(p => p.RespectLeft, _state.RespectLeft)
                        .SetProperty(p => p.PetRespectLeft, _state.PetRespectLeft)
                        .SetProperty(p => p.RespectReplenishesLeft, _state.RespectReplenishesLeft)
                        .SetProperty(
                            p => p.LastRespectReset,
                            _state.LastRespectReset == DateTime.MinValue
                                ? null
                                : (DateTime?)_state.LastRespectReset
                        ),
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
                CreatedAt = _state.CreatedAt,
                RespectTotal = _state.RespectTotal,
                RespectLeft = _state.RespectLeft,
                PetRespectLeft = _state.PetRespectLeft,
                RespectReplenishesLeft = _state.RespectReplenishesLeft,
            }
        );

    public Task<PlayerExtendedProfileSnapshot> GetExtendedProfileSnapshotAsync(CancellationToken ct)
    {
        return Task.FromResult(
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
                IsOnline = true,
                Guilds = [],
                LastAccessSinceInSeconds = 0,
                OpenProfileWindow = true,
                IsHidden = false,
                AccountLevel = 1,
                IntegerField24 = 0,
                StarGemCount = 0,
                RespectTotal = _state.RespectTotal,
                BooleanField26 = false,
                BooleanField27 = false,
            }
        );
    }

    public async Task<int> ReceiveRespectAsync(PlayerId giverId, CancellationToken ct)
    {
        _state.RespectTotal++;
        await WriteToDatabaseAsync(ct);

        return _state.RespectTotal;
    }

    public async Task<bool> TryConsumeRespectAsync(CancellationToken ct)
    {
        await TryResetDailyRespectIfNeededAsync(ct);

        if (_state.RespectLeft <= 0)
            return false;

        _state.RespectLeft--;
        await WriteToDatabaseAsync(ct);
        return true;
    }

    public async Task<bool> TryReplenishRespectAsync(int maxRespectPerDay, CancellationToken ct)
    {
        await TryResetDailyRespectIfNeededAsync(ct);

        if (_state.RespectReplenishesLeft <= 0)
            return false;

        _state.RespectReplenishesLeft--;
        _state.RespectLeft = maxRespectPerDay;
        await WriteToDatabaseAsync(ct);

        return true;
    }

    public async Task ResetDailyRespectAsync(
        int dailyRespectAmount,
        int dailyPetRespectAmount,
        int dailyReplenishLimit,
        CancellationToken ct
    )
    {
        _state.RespectLeft = dailyRespectAmount;
        _state.PetRespectLeft = dailyPetRespectAmount;
        _state.RespectReplenishesLeft = dailyReplenishLimit;
        _state.LastRespectReset = DateTime.UtcNow;
        await WriteToDatabaseAsync(ct);
    }

    /// <summary>
    /// Lazy daily reset: if the last reset was before today's midnight (UTC),
    /// automatically replenish daily respect allowances from configuration.
    /// This avoids the need for a global scheduled task to iterate all players.
    /// </summary>
    private async Task TryResetDailyRespectIfNeededAsync(CancellationToken ct)
    {
        var todayMidnight = DateTime.UtcNow.Date;

        if (_state.LastRespectReset >= todayMidnight)
            return;

        var dailyRespect = _configuration.GetValue("Turbo:Respect:DailyRespectAmount", 3);
        var dailyPetRespect = _configuration.GetValue("Turbo:Respect:DailyPetRespectAmount", 3);
        var dailyReplenish = _configuration.GetValue("Turbo:Respect:DailyReplenishLimit", 1);

        await ResetDailyRespectAsync(dailyRespect, dailyPetRespect, dailyReplenish, ct);
    }
}
