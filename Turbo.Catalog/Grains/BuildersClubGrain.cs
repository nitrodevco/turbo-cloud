using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Catalog.Configuration;
using Turbo.Database.Context;
using Turbo.Players.Configuration;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Catalog.Grains;

/// <summary>
/// The Builders Club for the whole hotel, one grain: how much furni each player has borrowed. It
/// is a read-through cache over <c>builders_club_furniture</c>, counted on activation and again
/// on a timer, with borrows and returns adjusting the counts in between. Nothing here is written
/// back — the rooms own the rows — so there is nothing to flush on deactivation.
///
/// A player's borrows are spread across their rooms, so this is the only place that can answer
/// how many they hold. Every placement asks, so it answers from memory and the only queries are
/// the recount. Kept alive because reactivating means counting every borrowed furni again while
/// those placements wait.
/// </summary>
[KeepAlive]
internal sealed partial class BuildersClubGrain : Grain, IBuildersClubGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly BuildersClubConfig _buildersClubConfig;
    private readonly SubscriptionConfig _subscriptionConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IBuildersClubGrain> _logger;

    private readonly BuildersClubLiveState _state = new();

    private IDisposable? _refreshTimer;
    private IDisposable? _lapseSweepTimer;

    public BuildersClubGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<CatalogConfig> catalogConfig,
        IOptions<PlayerConfig> playerConfig,
        IGrainFactory grainFactory,
        ILogger<IBuildersClubGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _buildersClubConfig = catalogConfig.Value.BuildersClub;
        // How long a lapsed membership keeps lending is the subscription's own rule.
        _subscriptionConfig = playerConfig.Value.Subscriptions;
        _grainFactory = grainFactory;
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
            _logger.LogError(ex, "Failed to hydrate the Builders Club borrow counts");

            throw;
        }

        _refreshTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) => await ((BuildersClubGrain)self!).RefreshAsync(ct),
            this,
            TimeSpan.FromMilliseconds(_buildersClubConfig.BorrowedCountRefreshMs),
            TimeSpan.FromMilliseconds(_buildersClubConfig.BorrowedCountRefreshMs)
        );

        _lapseSweepTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) => await ((BuildersClubGrain)self!).SweepAsync(ct),
            this,
            TimeSpan.FromMilliseconds(_buildersClubConfig.LapseSweepMs),
            TimeSpan.FromMilliseconds(_buildersClubConfig.LapseSweepMs)
        );
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;

        _lapseSweepTimer?.Dispose();
        _lapseSweepTimer = null;

        return Task.CompletedTask;
    }

    public Task<int> GetBorrowedCountAsync(PlayerId playerId, CancellationToken ct) =>
        Task.FromResult(_state.BorrowedCountByPlayerId.GetValueOrDefault(playerId));

    public Task SendFurniCountAsync(PlayerId playerId, CancellationToken ct) =>
        _grainFactory.SendComposerToPlayerAsync(
            playerId,
            new BuildersClubFurniCountMessageComposer
            {
                FurniCount = _state.BorrowedCountByPlayerId.GetValueOrDefault(playerId),
            },
            ct
        );

    public Task OnBorrowedAsync(PlayerId playerId, CancellationToken ct)
    {
        _state.BorrowedCountByPlayerId[playerId] =
            _state.BorrowedCountByPlayerId.GetValueOrDefault(playerId) + 1;

        return SendFurniCountAsync(playerId, ct);
    }

    public Task OnReturnedAsync(PlayerId playerId, int count, CancellationToken ct)
    {
        if (count <= 0)
            return Task.CompletedTask;

        var remaining = _state.BorrowedCountByPlayerId.GetValueOrDefault(playerId) - count;

        if (remaining > 0)
            _state.BorrowedCountByPlayerId[playerId] = remaining;
        else
            _state.BorrowedCountByPlayerId.Remove(playerId);

        return SendFurniCountAsync(playerId, ct);
    }

    /// <summary>
    /// Recounts from the rows. A borrow that was counted here but whose row never landed, or a
    /// room that was deleted from outside, drifts the count; the rows are the truth.
    /// </summary>
    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            await HydrateAsync(ct);
        }
        catch (Exception ex)
        {
            // One bad recount must not stop the next: the counts stay as they were.
            _logger.LogError(ex, "Failed to recount the Builders Club borrow counts");
        }
    }

    private async Task HydrateAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var counts = await dbCtx
            .BuildersClubFurnitures.AsNoTracking()
            .GroupBy(x => x.PlacedByPlayerEntityId)
            .Select(g => new { PlayerId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        _state.BorrowedCountByPlayerId.Clear();

        foreach (var entry in counts)
            _state.BorrowedCountByPlayerId[entry.PlayerId] = entry.Count;
    }
}
