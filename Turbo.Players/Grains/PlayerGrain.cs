using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Orleans.Runtime;
using Turbo.Database.Context;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Orleans.Snapshots.Players;
using Turbo.Primitives.Orleans.States.Players;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Players.Grains;

internal sealed class PlayerGrain(
    [PersistentState(OrleansStateNames.PLAYER_STATE, OrleansStorageNames.PLAYER_STORE)]
        IPersistentState<PlayerState> state,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory
) : Grain, IPlayerGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly IGrainFactory _grainFactory = grainFactory;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        await HydrateAsync(ct);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        await WriteToDatabaseAsync(ct);
    }

    private async Task HydrateAsync(CancellationToken ct)
    {
        if (state.State.IsLoaded)
            return;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entity =
            await dbCtx
                .Players.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == (int)this.GetPrimaryKeyLong(), ct)
            ?? throw new TurboException(TurboErrorCodeEnum.PlayerNotFound);

        state.State.Name = entity.Name ?? string.Empty;
        state.State.Motto = entity.Motto ?? string.Empty;
        state.State.Figure = entity.Figure ?? string.Empty;
        state.State.Gender = entity.Gender;
        state.State.CreatedAt = entity.CreatedAt;
        state.State.LastUpdated = DateTime.UtcNow;
        state.State.IsLoaded = true;

        await _grainFactory
            .GetPlayerDirectoryGrain()
            .SetPlayerNameAsync(
                PlayerId.Parse((int)this.GetPrimaryKeyLong()),
                state.State.Name,
                ct
            );

        await state.WriteStateAsync(ct);
    }

    private async Task WriteToDatabaseAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var snapshot = await GetSummaryAsync(ct);

        await dbCtx
            .Players.Where(p => p.Id == (int)this.GetPrimaryKeyLong())
            .ExecuteUpdateAsync(
                up =>
                    up.SetProperty(p => p.Name, snapshot.Name)
                        .SetProperty(p => p.Motto, snapshot.Motto)
                        .SetProperty(p => p.Figure, snapshot.Figure)
                        .SetProperty(p => p.Gender, snapshot.Gender),
                ct
            );
    }

    public Task<PlayerSummarySnapshot> GetSummaryAsync(CancellationToken ct) =>
        Task.FromResult(
            new PlayerSummarySnapshot
            {
                PlayerId = PlayerId.Parse((int)this.GetPrimaryKeyLong()),
                Name = state.State.Name,
                Motto = state.State.Motto,
                Figure = state.State.Figure,
                Gender = state.State.Gender,
                CreatedAt = state.State.CreatedAt,
            }
        );

    public async Task<PlayerWalletSnapshot> GetWalletAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var playerId = (int)this.GetPrimaryKeyLong();
        var currencies = await dbCtx
            .PlayerCurrencies.AsNoTracking()
            .Where(x => x.PlayerEntityId == playerId)
            .Include(x => x.CurrencyTypeEntity)
            .ToListAsync(ct);

        var credits = currencies
            .Where(x =>
                IsCurrencyKey(
                    x.CurrencyTypeEntity?.CurrencyKey,
                    WalletCurrencyKeyMapper.Credits,
                    "credit"
                ) || WalletCurrencyKeyMapper.IsCreditsKey(x.Type)
            )
            .Sum(x => x.Amount);

        var activityPoints = currencies
            .Select(x => new
            {
                CategoryId = ResolveActivityPointCategory(
                    x.CurrencyTypeEntity?.IsActivityPoints,
                    x.CurrencyTypeEntity?.ActivityPointType,
                    x.Type
                ),
                x.Amount,
            })
            .Where(x => x.CategoryId.HasValue)
            .GroupBy(x => x.CategoryId!.Value)
            .ToImmutableDictionary(g => g.Key, g => g.Sum(x => x.Amount));

        var emeralds = currencies
            .Where(x =>
                IsCurrencyKey(x.CurrencyTypeEntity?.CurrencyKey, "emerald", "emeralds")
                || string.Equals(x.Type, "emerald", StringComparison.OrdinalIgnoreCase)
            )
            .Sum(x => x.Amount);
        var silver = currencies
            .Where(x =>
                IsCurrencyKey(x.CurrencyTypeEntity?.CurrencyKey, "silver")
                || string.Equals(x.Type, "silver", StringComparison.OrdinalIgnoreCase)
            )
            .Sum(x => x.Amount);

        return new PlayerWalletSnapshot
        {
            Credits = credits,
            ActivityPointsByCategoryId = activityPoints,
            Emeralds = emeralds,
            Silver = silver,
        };
    }

    private static bool IsCurrencyKey(string? value, params string[] keys) =>
        !string.IsNullOrWhiteSpace(value)
        && keys.Any(x => string.Equals(value, x, StringComparison.OrdinalIgnoreCase));

    private static int? ResolveActivityPointCategory(
        bool? isActivityPoints,
        int? activityPointType,
        string type
    )
    {
        if (isActivityPoints == true && activityPointType.HasValue && activityPointType.Value >= 0)
            return activityPointType.Value;

        return WalletCurrencyKeyMapper.TryGetActivityPointType(type, out var categoryId)
            ? categoryId
            : null;
    }

    public Task<PlayerExtendedProfileSnapshot> GetExtendedProfileSnapshotAsync(CancellationToken ct)
    {
        var s = state.State;
        return Task.FromResult(
            new PlayerExtendedProfileSnapshot
            {
                UserId = PlayerId.Parse((int)this.GetPrimaryKeyLong()),
                UserName = s.Name,
                Figure = s.Figure,
                Motto = s.Motto,
                CreationDate = s.CreatedAt.ToString("yyyy-MM-dd"),
                AchievementScore = 0,
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
                BooleanField26 = false,
                BooleanField27 = false,
            }
        );
    }
}
