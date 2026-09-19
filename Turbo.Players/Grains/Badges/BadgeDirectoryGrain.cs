using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Players.Badges;
using Turbo.Players.Configuration;
using Turbo.Primitives.Badges.Enums;
using Turbo.Primitives.Badges.Grains;
using Turbo.Primitives.Badges.Snapshots;

namespace Turbo.Players.Grains.Badges;

/// <summary>
/// Badge figures for the whole hotel, one grain: owner counts per code and the rarity they give.
/// It is a read-through cache: the player_badges rows are the truth, counted on activation and
/// again on a timer, with grants and revokes adjusting the counts in between. Nothing here is
/// written back, so there is nothing to flush on deactivation.
///
/// Every badge shown anywhere asks this grain, so its methods answer from memory and the only
/// queries are the recount. Anything that queries per request (the leaderboards) lives in
/// <see cref="BadgeLeaderboardGrain"/>. Kept alive because reactivating means counting every
/// badge row again while all of those reads wait.
/// </summary>
[KeepAlive]
internal sealed class BadgeDirectoryGrain : Grain, IBadgeDirectoryGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly BadgeConfig _badgeConfig;
    private readonly ILogger<IBadgeDirectoryGrain> _logger;

    private readonly BadgeDirectoryLiveState _state = new();

    private IDisposable? _refreshTimer;

    public BadgeDirectoryGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<BadgeConfig> badgeConfig,
        ILogger<IBadgeDirectoryGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _badgeConfig = badgeConfig.Value;
        _logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hydrate the badge directory");

            throw;
        }

        _refreshTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) => await ((BadgeDirectoryGrain)self!).RefreshAsync(ct),
            this,
            TimeSpan.FromMilliseconds(_badgeConfig.OwnerCountRefreshMs),
            TimeSpan.FromMilliseconds(_badgeConfig.OwnerCountRefreshMs)
        );
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;

        return Task.CompletedTask;
    }

    public Task<ImmutableArray<BadgeInfoSnapshot>> GetInfoAsync(
        ImmutableArray<string> badgeCodes,
        CancellationToken ct
    ) => Task.FromResult(badgeCodes.Select(GetInfo).ToImmutableArray());

    public Task<ImmutableArray<string>> GetCodesOfRarityAsync(
        BadgeRarityType rarity,
        CancellationToken ct
    ) =>
        Task.FromResult(
            _state
                .OwnerCountByCode.Keys.Union(
                    _state.PinnedRarityByCode.Keys,
                    StringComparer.OrdinalIgnoreCase
                )
                .Where(code => GetInfo(code).Rarity == rarity)
                .ToImmutableArray()
        );

    public Task<BadgeInfoSnapshot> OnBadgeGrantedAsync(string badgeCode, CancellationToken ct)
    {
        _state.OwnerCountByCode[badgeCode] =
            _state.OwnerCountByCode.GetValueOrDefault(badgeCode) + 1;

        return Task.FromResult(GetInfo(badgeCode));
    }

    public Task OnBadgeRevokedAsync(string badgeCode, CancellationToken ct)
    {
        var remaining = _state.OwnerCountByCode.GetValueOrDefault(badgeCode) - 1;

        if (remaining > 0)
            _state.OwnerCountByCode[badgeCode] = remaining;
        else
            _state.OwnerCountByCode.Remove(badgeCode);

        return Task.CompletedTask;
    }

    public Task<string?> GetRequestableBadgeAsync(string requestCode, CancellationToken ct) =>
        Task.FromResult(
            !string.IsNullOrEmpty(requestCode)
            && _badgeConfig.RequestableBadges.TryGetValue(requestCode, out var badgeCode)
                ? badgeCode
                : null
        );

    private BadgeInfoSnapshot GetInfo(string badgeCode)
    {
        var ownerCount = _state.OwnerCountByCode.GetValueOrDefault(badgeCode);

        return new BadgeInfoSnapshot
        {
            BadgeCode = badgeCode,
            OwnerCount = ownerCount,
            Rarity = _state.PinnedRarityByCode.TryGetValue(badgeCode, out var pinned)
                ? pinned
                : BadgeRarityCalculator.Calculate(_badgeConfig, ownerCount, _state.TotalPlayers),
        };
    }

    /// <summary>The timer's recount. A failure keeps the counts it had and tries again next time.</summary>
    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recount badge owners; keeping the previous counts");
        }
    }

    private async Task HydrateAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var counts = await dbCtx
            .PlayerBadges.AsNoTracking()
            .GroupBy(x => x.BadgeCode)
            .Select(g => new { BadgeCode = g.Key, Owners = g.Count() })
            .ToListAsync(ct);
        var pinned = await dbCtx
            .BadgeDefinitions.AsNoTracking()
            .Where(x => x.Rarity != null)
            .Select(x => new { x.BadgeCode, Rarity = x.Rarity!.Value })
            .ToListAsync(ct);
        var totalPlayers = await dbCtx.Players.CountAsync(ct);

        _state.OwnerCountByCode.Clear();

        foreach (var count in counts)
            _state.OwnerCountByCode[count.BadgeCode] = count.Owners;

        _state.PinnedRarityByCode.Clear();

        foreach (var definition in pinned)
            _state.PinnedRarityByCode[definition.BadgeCode] = definition.Rarity;

        _state.TotalPlayers = totalPlayers;
    }
}
