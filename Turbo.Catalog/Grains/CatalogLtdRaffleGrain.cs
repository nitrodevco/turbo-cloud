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
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Extensions;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Messages.Outgoing.Collectibles;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Catalog.Grains;

/// <summary>
/// The sale of one limited series, keyed by the series id, so concurrent buyers are serialized.
/// The series row is loaded on activation; entrants of the running batch live in memory only
/// and results are written through when the raffle runs. Deactivation runs a pending raffle
/// rather than lose its entrants.
/// </summary>
internal sealed class CatalogLtdRaffleGrain : Grain, ICatalogLtdRaffleGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly CatalogConfig _config;
    private readonly IGrainFactory _grainFactory;
    private readonly ICatalogService _catalogService;
    private readonly ILogger<ICatalogLtdRaffleGrain> _logger;

    private readonly CatalogLtdRaffleLiveState _state;

    private IDisposable? _raffleTimer;

    public CatalogLtdRaffleGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<CatalogConfig> config,
        IGrainFactory grainFactory,
        ICatalogService catalogService,
        ILogger<ICatalogLtdRaffleGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _config = config.Value;
        _grainFactory = grainFactory;
        _catalogService = catalogService;
        _logger = logger;

        // The grain is keyed by the limited series it raffles.
        _state = new() { SeriesId = (int)this.GetPrimaryKeyLong() };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await ReloadSeriesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to hydrate ltd raffle series {SeriesId}", _state.SeriesId);

            throw;
        }

        if (_state.Series != null)
            _state.RaffleFinished = _state.Series.IsRaffleFinished;
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _raffleTimer?.Dispose();
        _raffleTimer = null;

        if (_state.CurrentBatchEntries.Count > 0)
        {
            try
            {
                await ExecuteRaffleAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to execute raffle during deactivation for series {SeriesId}",
                    _state.SeriesId
                );
            }
        }
    }

    public async Task<LtdRaffleEntryResult> EnterRaffleAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        if (_state.Series is not { IsAvailable: true })
        {
            return LtdRaffleEntryResult.Failed(
                _state.Series?.RemainingQuantity <= 0
                    ? LtdRaffleEntryErrorType.SoldOut
                    : LtdRaffleEntryErrorType.SeriesNotFound
            );
        }

        var snap = _catalogService.GetCatalogSnapshot(CatalogType.Normal);
        var product = snap.ProductsById.Values.FirstOrDefault(p =>
            p.LtdSeriesId == _state.Series.Id
        );

        if (product == null || !snap.OffersById.TryGetValue(product.OfferId, out var offer))
            return LtdRaffleEntryResult.Failed(LtdRaffleEntryErrorType.None);

        var walletGrain = _grainFactory.GetPlayerWalletGrain(playerId);
        var credits = await walletGrain.GetAmountForCurrencyAsync(
            new CurrencyKind { CurrencyType = CurrencyType.Credits },
            ct
        );
        var activityPoints = await walletGrain.GetActivityPointsAsync(ct);

        var hasInsufficientCredits = offer.CostCredits > credits;
        var hasInsufficientActivityPoints =
            offer is { CostCurrency: > 0, CurrencyTypeId: not null }
            && activityPoints.GetValueOrDefault(offer.CurrencyTypeId.Value) < offer.CostCurrency;

        if (hasInsufficientCredits || hasInsufficientActivityPoints)
        {
            return LtdRaffleEntryResult.Failed(
                LtdRaffleEntryErrorType.InsufficientFunds,
                new CatalogBalanceFailure
                {
                    NotEnoughCredits = hasInsufficientCredits,
                    NotEnoughActivityPoints = hasInsufficientActivityPoints,
                    ActivityPointType = offer.CurrencyTypeId ?? 0,
                }
            );
        }

        var buffer =
            _state.Series.RaffleWindowSeconds > 0
                ? _state.Series.RaffleWindowSeconds
                : _config.LtdRaffle.DefaultBufferSeconds;

        if (buffer <= 0 || _state.RaffleFinished)
        {
            var instantWin = await TryFinalizeWinnerAsync(playerId, null, false, ct);
            return instantWin
                ? LtdRaffleEntryResult.Succeeded("instant")
                : LtdRaffleEntryResult.Failed(LtdRaffleEntryErrorType.SoldOut);
        }

        if (_config.LtdRaffle.LimitOnePerCustomer)
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var alreadyWon = await dbCtx.LtdRaffleEntries.AnyAsync(
                e =>
                    e.SeriesEntityId == _state.Series.Id
                    && e.PlayerEntityId == playerId
                    && e.Result == "won",
                ct
            );

            if (alreadyWon)
                return LtdRaffleEntryResult.Failed(LtdRaffleEntryErrorType.AlreadyWon);
        }

        if (_state.CurrentBatchEntries.ContainsKey(playerId))
            return LtdRaffleEntryResult.Failed(LtdRaffleEntryErrorType.AlreadyInQueue);

        if (_state.CurrentBatchId == null)
        {
            _state.CurrentBatchId = Guid.NewGuid().ToString();
            _state.IsInBufferPeriod = true;
            _raffleTimer = this.RegisterGrainTimer<object?>(
                static async (self, ct) =>
                    await ((CatalogLtdRaffleGrain)self!).ExecuteRaffleAsync(ct),
                this,
                TimeSpan.FromSeconds(buffer),
                Timeout.InfiniteTimeSpan
            );
        }

        if (!_state.IsInBufferPeriod)
            return LtdRaffleEntryResult.Failed(LtdRaffleEntryErrorType.RaffleProcessing);

        if (_state.CurrentBatchEntries.Count >= _config.LtdRaffle.MaxEntriesPerBatch)
            return LtdRaffleEntryResult.Failed(LtdRaffleEntryErrorType.RaffleProcessing);

        _state.CurrentBatchEntries[playerId] = await CalculateWeightAsync(playerId, ct);
        await PersistEntryAsync(playerId, _state.CurrentBatchId, ct);

        await _grainFactory.SendComposerToPlayerAsync(
            playerId,
            new LtdRaffleEnteredMessageComposer { ClassName = product.ClassName ?? "LTD" },
            CancellationToken.None
        );

        return LtdRaffleEntryResult.Succeeded(_state.CurrentBatchId);
    }

    private async Task ExecuteRaffleAsync(CancellationToken ct)
    {
        if (_state.CurrentBatchId == null || _state.CurrentBatchEntries.Count == 0)
        {
            _state.IsInBufferPeriod = false;
            return;
        }

        var batchId = _state.CurrentBatchId;
        var entries = _state.CurrentBatchEntries.ToList();

        _state.CurrentBatchId = null;
        _state.CurrentBatchEntries.Clear();
        _state.IsInBufferPeriod = false;
        _state.RaffleFinished = true;
        _raffleTimer?.Dispose();
        _raffleTimer = null;

        await PersistFinishedAsync();
        await ReloadSeriesAsync(ct);

        var winnersCount = Math.Min(entries.Count, _state.Series?.RemainingQuantity ?? 0);
        var winners = _config.LtdRaffle.UsePureRandom
            ? [.. entries.OrderBy(_ => Random.Shared.Next()).Take(winnersCount).Select(e => e.Key)]
            : SelectWeighted(entries, winnersCount);

        var loserIds = new List<int>();

        // Winners must be sequential (row lock + quantity decrement per winner)
        foreach (var entry in entries)
        {
            if (winners.Contains(entry.Key))
                await TryFinalizeWinnerAsync(entry.Key, batchId, true, ct);
            else
                loserIds.Add(entry.Key);
        }

        // Loser notifications go to different presence grains — parallelize
        if (loserIds.Count > 0)
        {
            await Task.WhenAll(
                loserIds.Select(id => NotifyLoserAsync(id, LtdRaffleResultType.Lost, ct))
            );
        }

        if (loserIds.Count > 0)
        {
            await using var db = await _dbCtxFactory.CreateDbContextAsync(CancellationToken.None);

            await db
                .LtdRaffleEntries.Where(e =>
                    e.BatchId == batchId && loserIds.Contains(e.PlayerEntityId)
                )
                .ExecuteUpdateAsync(u =>
                    u.SetProperty(e => e.Result, "lost")
                        .SetProperty(e => e.ProcessedAt, DateTime.UtcNow)
                );
        }

        await ReloadSeriesAsync(CancellationToken.None);
    }

    private async Task<bool> TryFinalizeWinnerAsync(
        int playerId,
        string? batchId,
        bool isRaffle,
        CancellationToken ct
    )
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync();
        await using var tx = await dbCtx.Database.BeginTransactionAsync();

        try
        {
            var series = await dbCtx
                .LtdSeries.FromSqlRaw(
                    "SELECT * FROM ltd_series WHERE id = {0} FOR UPDATE",
                    _state.SeriesId
                )
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync();

            if (series is not { RemainingQuantity: > 0 })
                return false;

            var snap = _catalogService.GetCatalogSnapshot(CatalogType.Normal);
            var prod = snap.ProductsById.Values.First(p => p.LtdSeriesId == series.Id);
            var offer = snap.OffersById[prod.OfferId];

            var debitResult = await _grainFactory
                .GetPlayerWalletGrain(playerId)
                .TryDebitAsync(BuildDebits(offer), ct);

            if (!debitResult.Succeeded)
            {
                await NotifyLoserAsync(playerId, LtdRaffleResultType.Lost, ct);
                return false;
            }

            var serial = _config.LtdRaffle.RandomizeSerials
                ? (await GetAvailableSerialsAsync(dbCtx, series))[
                    Random.Shared.Next(series.RemainingQuantity)
                ]
                : (series.TotalQuantity - series.RemainingQuantity) + 1;

            series.RemainingQuantity--;

            if (batchId != null)
            {
                var entry = await dbCtx
                    .LtdRaffleEntries.OrderBy(e => e.Id)
                    .FirstOrDefaultAsync(e => e.BatchId == batchId && e.PlayerEntityId == playerId);

                if (entry != null)
                {
                    entry.Result = "won";
                    entry.SerialNumber = serial;
                    entry.ProcessedAt = DateTime.UtcNow;
                }
            }

            await dbCtx.SaveChangesAsync();
            await tx.CommitAsync();

            await _grainFactory
                .GetInventoryGrain(playerId)
                .GrantLtdFurnitureAsync(
                    series.CatalogProductEntityId,
                    serial,
                    series.TotalQuantity,
                    CancellationToken.None
                );

            if (isRaffle)
            {
                await _grainFactory.SendComposerToPlayerAsync(
                    playerId,
                    new LtdRaffleResultMessageComposer
                    {
                        ClassName = prod.ClassName ?? "LTD",
                        ResultCode = LtdRaffleResultType.Won,
                    },
                    CancellationToken.None
                );
            }
            else
            {
                await _grainFactory.SendComposerToPlayerAsync(
                    playerId,
                    new PurchaseOKMessageComposer { Offer = offer },
                    CancellationToken.None
                );
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to finalize LTD raffle winner for player {PlayerId} in series {SeriesId}",
                playerId,
                _state.SeriesId
            );

            await tx.RollbackAsync();
            return false;
        }
    }

    private async Task<List<int>> GetAvailableSerialsAsync(TurboDbContext db, LtdSeriesEntity s)
    {
        var usedSerials = await db
            .LtdRaffleEntries.Where(e =>
                e.SeriesEntityId == s.Id && e.Result == "won" && e.SerialNumber != null
            )
            .Select(e => e.SerialNumber!.Value)
            .ToListAsync();

        return [.. Enumerable.Range(1, s.TotalQuantity).Except(usedSerials)];
    }

    private static List<WalletDebitRequest> BuildDebits(CatalogOfferSnapshot offer)
    {
        var debits = new List<WalletDebitRequest>();

        if (offer.CostCredits > 0)
        {
            debits.Add(
                new WalletDebitRequest
                {
                    CurrencyKind = new CurrencyKind { CurrencyType = CurrencyType.Credits },
                    Amount = offer.CostCredits,
                }
            );
        }

        if (offer.CostCurrency > 0)
        {
            debits.Add(
                new WalletDebitRequest
                {
                    CurrencyKind = new CurrencyKind
                    {
                        CurrencyType = CurrencyType.ActivityPoints,
                        ActivityPointType = offer.CurrencyTypeId,
                    },
                    Amount = offer.CostCurrency,
                }
            );
        }

        return debits;
    }

    private async Task<double> CalculateWeightAsync(int playerId, CancellationToken ct)
    {
        var playerGrain = _grainFactory.GetPlayerGrain(PlayerId.Parse(playerId));
        var summary = await playerGrain.GetSummaryAsync(ct);
        var profile = await playerGrain.GetExtendedProfileSnapshotAsync(ct);

        var cfg = _config.LtdRaffle;
        var weight = cfg.BaseWeight;

        // TODO: Replace DB queries with snapshot-based lookups once PlayerGrain exposes
        // badge count, room count, and furniture count in PlayerSummarySnapshot or a dedicated snapshot.
        var needsDbQuery =
            cfg.BadgeCount.Enabled || cfg.RoomCount.Enabled || cfg.FurnitureCount.Enabled;

        if (needsDbQuery)
        {
            await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);

            if (cfg.BadgeCount.Enabled)
            {
                var badgeCount = await db.PlayerBadges.CountAsync(
                    b => b.PlayerEntityId == playerId,
                    ct
                );
                weight += Math.Min(
                    badgeCount * cfg.BadgeCount.BonusPerUnit,
                    cfg.BadgeCount.MaxBonus
                );
            }

            if (cfg.RoomCount.Enabled)
            {
                var roomCount = await db.Rooms.CountAsync(r => r.PlayerEntityId == playerId, ct);
                weight += Math.Min(roomCount * cfg.RoomCount.BonusPerUnit, cfg.RoomCount.MaxBonus);
            }

            if (cfg.FurnitureCount.Enabled)
            {
                var furniCount = await db.Furnitures.CountAsync(
                    f => f.PlayerEntityId == playerId,
                    ct
                );
                weight += Math.Min(
                    furniCount * cfg.FurnitureCount.BonusPerUnit,
                    cfg.FurnitureCount.MaxBonus
                );
            }
        }

        // Snapshot-based weighting (no DB round-trip needed)
        if (cfg.AccountAgeDays.Enabled)
            weight += Math.Min(
                (DateTime.UtcNow - summary.CreatedAt).Days * cfg.AccountAgeDays.BonusPerUnit,
                cfg.AccountAgeDays.MaxBonus
            );

        if (cfg.AchievementScore.Enabled)
            weight += Math.Min(
                profile.AchievementScore * cfg.AchievementScore.BonusPerUnit,
                cfg.AchievementScore.MaxBonus
            );

        if (cfg.FriendCount.Enabled)
            weight += Math.Min(
                profile.FriendCount * cfg.FriendCount.BonusPerUnit,
                cfg.FriendCount.MaxBonus
            );

        if (cfg.RespectsReceived.Enabled)
            weight += Math.Min(
                profile.StarGemCount * cfg.RespectsReceived.BonusPerUnit,
                cfg.RespectsReceived.MaxBonus
            );

        return weight;
    }

    private static HashSet<int> SelectWeighted(List<KeyValuePair<int, double>> entries, int count)
    {
        var winners = new HashSet<int>();
        var pool = entries.ToList();

        for (var i = 0; i < count && pool.Count > 0; i++)
        {
            var total = pool.Sum(e => e.Value);
            var roll = Random.Shared.NextDouble() * total;
            var current = 0.0;

            foreach (var entry in pool)
            {
                current += entry.Value;

                if (roll <= current)
                {
                    winners.Add(entry.Key);
                    pool.Remove(entry);
                    break;
                }
            }
        }

        return winners;
    }

    public async Task ReloadSeriesAsync(CancellationToken ct)
    {
        await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entity = await db
            .LtdSeries.AsNoTracking()
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(s => s.Id == _state.SeriesId, ct);

        if (entity != null)
            _state.Series = entity.ToSnapshot();
    }

    private async Task PersistFinishedAsync()
    {
        await using var db = await _dbCtxFactory.CreateDbContextAsync();

        await db
            .LtdSeries.Where(s => s.Id == _state.SeriesId)
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.IsRaffleFinished, true));
    }

    private async Task NotifyLoserAsync(
        int playerId,
        LtdRaffleResultType resultCode,
        CancellationToken ct
    )
    {
        var product = _catalogService
            .GetCatalogSnapshot(CatalogType.Normal)
            .ProductsById.Values.FirstOrDefault(p => p.LtdSeriesId == _state.Series?.Id);

        await _grainFactory.SendComposerToPlayerAsync(
            playerId,
            new LtdRaffleResultMessageComposer
            {
                ClassName = product?.ClassName ?? "LTD",
                ResultCode = resultCode,
            },
            ct
        );
    }

    private async Task PersistEntryAsync(int playerId, string batchId, CancellationToken ct)
    {
        await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);

        db.LtdRaffleEntries.Add(
            new LtdRaffleEntryEntity
            {
                SeriesEntityId = _state.SeriesId,
                PlayerEntityId = playerId,
                BatchId = batchId,
                EnteredAt = DateTime.UtcNow,
                Result = "pending",
            }
        );

        await db.SaveChangesAsync(ct);
    }

    public Task<LtdSeriesSnapshot?> GetSeriesSnapshotAsync(CancellationToken ct) =>
        Task.FromResult(_state.Series);

    public async Task ForceRunRaffleAsync(CancellationToken ct)
    {
        _raffleTimer?.Dispose();
        await ExecuteRaffleAsync(ct);
    }
}
