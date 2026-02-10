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

public sealed class LtdRaffleGrain(
    IGrainFactory grainFactory,
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    ILogger<LtdRaffleGrain> logger,
    ICatalogService catalogService,
    IOptions<CatalogConfig> config
) : Grain, ILtdRaffleGrain
{
    private readonly CatalogConfig _config = config.Value;

    private readonly Dictionary<int, double> _currentBatchEntries = [];

    private LtdSeriesSnapshot? _series;
    private string? _currentBatchId;
    private bool _isInBufferPeriod;
    private bool _raffleFinished;
    private IDisposable? _raffleTimer;

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        await ReloadSeriesAsync(ct);

        if (_series != null)
            _raffleFinished = _series.IsRaffleFinished;
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _raffleTimer?.Dispose();
        _raffleTimer = null;

        if (_currentBatchEntries.Count > 0)
        {
            try
            {
                await ExecuteRaffleAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to execute raffle during deactivation for series {SeriesId}",
                    this.GetPrimaryKeyLong()
                );
            }
        }
    }

    public async Task<LtdRaffleEntryResult> EnterRaffleAsync(int playerId, CancellationToken ct)
    {
        if (_series is not { IsAvailable: true })
        {
            return LtdRaffleEntryResult.Failed(
                _series?.RemainingQuantity <= 0
                    ? LtdRaffleEntryError.SoldOut
                    : LtdRaffleEntryError.SeriesNotFound
            );
        }

        var snap = catalogService.GetCatalogSnapshot(CatalogType.Normal);
        var product = snap.ProductsById.Values.FirstOrDefault(p => p.LtdSeriesId == _series.Id);

        if (product == null || !snap.OffersById.TryGetValue(product.OfferId, out var offer))
            return LtdRaffleEntryResult.Failed(LtdRaffleEntryError.None);

        var walletGrain = grainFactory.GetPlayerWalletGrain(playerId);
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
                LtdRaffleEntryError.InsufficientFunds,
                new CatalogBalanceFailure
                {
                    NotEnoughCredits = hasInsufficientCredits,
                    NotEnoughActivityPoints = hasInsufficientActivityPoints,
                    ActivityPointType = offer.CurrencyTypeId ?? 0,
                }
            );
        }

        var buffer =
            _series.RaffleWindowSeconds > 0
                ? _series.RaffleWindowSeconds
                : _config.LtdRaffle.DefaultBufferSeconds;

        if (buffer <= 0 || _raffleFinished)
        {
            var instantWin = await TryFinalizeWinnerAsync(playerId, null, false);
            return instantWin
                ? LtdRaffleEntryResult.Succeeded("instant")
                : LtdRaffleEntryResult.Failed(LtdRaffleEntryError.SoldOut);
        }

        if (_config.LtdRaffle.LimitOnePerCustomer)
        {
            await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

            var alreadyWon = await dbCtx.LtdRaffleEntries.AnyAsync(
                e =>
                    e.SeriesEntityId == _series.Id
                    && e.PlayerEntityId == playerId
                    && e.Result == "won",
                ct
            );

            if (alreadyWon)
                return LtdRaffleEntryResult.Failed(LtdRaffleEntryError.AlreadyWon);
        }

        if (_currentBatchEntries.ContainsKey(playerId))
            return LtdRaffleEntryResult.Failed(LtdRaffleEntryError.AlreadyInQueue);

        if (_currentBatchId == null)
        {
            _currentBatchId = Guid.NewGuid().ToString();
            _isInBufferPeriod = true;
            _raffleTimer = this.RegisterGrainTimer<object?>(
                async _ => await ExecuteRaffleAsync(),
                null,
                TimeSpan.FromSeconds(buffer),
                Timeout.InfiniteTimeSpan
            );
        }

        if (!_isInBufferPeriod)
            return LtdRaffleEntryResult.Failed(LtdRaffleEntryError.RaffleProcessing);

        if (_currentBatchEntries.Count >= _config.LtdRaffle.MaxEntriesPerBatch)
            return LtdRaffleEntryResult.Failed(LtdRaffleEntryError.RaffleProcessing);

        _currentBatchEntries[playerId] = await CalculateWeightAsync(playerId, ct);
        await PersistEntryAsync(playerId, _currentBatchId, ct);

        await grainFactory
            .GetPlayerPresenceGrain(playerId)
            .SendComposerAsync(
                new LtdRaffleEnteredMessageComposer { ClassName = product.ClassName ?? "LTD" }
            );

        return LtdRaffleEntryResult.Succeeded(_currentBatchId);
    }

    private async Task ExecuteRaffleAsync()
    {
        if (_currentBatchId == null || _currentBatchEntries.Count == 0)
        {
            _isInBufferPeriod = false;
            return;
        }

        var batchId = _currentBatchId;
        var entries = _currentBatchEntries.ToList();

        _currentBatchId = null;
        _currentBatchEntries.Clear();
        _isInBufferPeriod = false;
        _raffleFinished = true;
        _raffleTimer?.Dispose();
        _raffleTimer = null;

        await PersistFinishedAsync();
        await ReloadSeriesAsync(CancellationToken.None);

        var winnersCount = Math.Min(entries.Count, _series?.RemainingQuantity ?? 0);
        var winners = _config.LtdRaffle.UsePureRandom
            ? [.. entries.OrderBy(_ => Random.Shared.Next()).Take(winnersCount).Select(e => e.Key)]
            : SelectWeighted(entries, winnersCount);

        var loserIds = new List<int>();

        // Winners must be sequential (row lock + quantity decrement per winner)
        foreach (var entry in entries)
        {
            if (winners.Contains(entry.Key))
                await TryFinalizeWinnerAsync(entry.Key, batchId, true);
            else
                loserIds.Add(entry.Key);
        }

        // Loser notifications go to different presence grains — parallelize
        if (loserIds.Count > 0)
        {
            await Task.WhenAll(
                loserIds.Select(id => NotifyLoserAsync(id, LtdRaffleResultCode.Lost))
            );
        }

        if (loserIds.Count > 0)
        {
            await using var db = await dbCtxFactory.CreateDbContextAsync(CancellationToken.None);

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

    private async Task<bool> TryFinalizeWinnerAsync(int playerId, string? batchId, bool isRaffle)
    {
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync();
        await using var tx = await dbCtx.Database.BeginTransactionAsync();

        try
        {
            var series = await dbCtx
                .LtdSeries.FromSqlRaw(
                    "SELECT * FROM ltd_series WHERE id = {0} FOR UPDATE",
                    (int)this.GetPrimaryKeyLong()
                )
                .OrderBy(x => x.Id)
                .FirstOrDefaultAsync();

            if (series is not { RemainingQuantity: > 0 })
                return false;

            var snap = catalogService.GetCatalogSnapshot(CatalogType.Normal);
            var prod = snap.ProductsById.Values.First(p => p.LtdSeriesId == series.Id);
            var offer = snap.OffersById[prod.OfferId];

            var debitResult = await grainFactory
                .GetPlayerWalletGrain(playerId)
                .TryDebitAsync(BuildDebits(offer), CancellationToken.None);

            if (!debitResult.Succeeded)
            {
                await NotifyLoserAsync(playerId, LtdRaffleResultCode.Lost);
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

            await grainFactory
                .GetInventoryGrain(playerId)
                .GrantLtdFurnitureAsync(
                    series.CatalogProductEntityId,
                    serial,
                    series.TotalQuantity,
                    CancellationToken.None
                );

            var presence = grainFactory.GetPlayerPresenceGrain(playerId);

            if (isRaffle)
            {
                await presence.SendComposerAsync(
                    new LtdRaffleResultMessageComposer
                    {
                        ClassName = prod.ClassName ?? "LTD",
                        ResultCode = LtdRaffleResultCode.Won,
                    }
                );
            }
            else
            {
                await presence.SendComposerAsync(new PurchaseOKMessageComposer { Offer = offer });
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to finalize LTD raffle winner for player {PlayerId} in series {SeriesId}",
                playerId,
                this.GetPrimaryKeyLong()
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
        var playerGrain = grainFactory.GetPlayerGrain(PlayerId.Parse(playerId));
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
            await using var db = await dbCtxFactory.CreateDbContextAsync(ct);

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
                profile.RespectTotal * cfg.RespectsReceived.BonusPerUnit,
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
        await using var db = await dbCtxFactory.CreateDbContextAsync(ct);

        var entity = await db
            .LtdSeries.AsNoTracking()
            .OrderBy(s => s.Id)
            .FirstOrDefaultAsync(s => s.Id == (int)this.GetPrimaryKeyLong(), ct);

        if (entity != null)
        {
            _series = new LtdSeriesSnapshot
            {
                Id = entity.Id,
                CatalogProductId = entity.CatalogProductEntityId,
                TotalQuantity = entity.TotalQuantity,
                RemainingQuantity = entity.RemainingQuantity,
                RaffleWindowSeconds = entity.RaffleWindowSeconds,
                IsActive = entity.IsActive,
                IsRaffleFinished = entity.IsRaffleFinished,
                StartsAt = entity.StartsAt,
                EndsAt = entity.EndsAt,
            };
        }
    }

    private async Task PersistFinishedAsync()
    {
        await using var db = await dbCtxFactory.CreateDbContextAsync();

        await db
            .LtdSeries.Where(s => s.Id == (int)this.GetPrimaryKeyLong())
            .ExecuteUpdateAsync(u => u.SetProperty(s => s.IsRaffleFinished, true));
    }

    private async Task NotifyLoserAsync(int playerId, LtdRaffleResultCode resultCode)
    {
        var product = catalogService
            .GetCatalogSnapshot(CatalogType.Normal)
            .ProductsById.Values.FirstOrDefault(p => p.LtdSeriesId == _series?.Id);

        await grainFactory
            .GetPlayerPresenceGrain(playerId)
            .SendComposerAsync(
                new LtdRaffleResultMessageComposer
                {
                    ClassName = product?.ClassName ?? "LTD",
                    ResultCode = resultCode,
                }
            );
    }

    private async Task PersistEntryAsync(int playerId, string batchId, CancellationToken ct)
    {
        await using var db = await dbCtxFactory.CreateDbContextAsync(ct);

        db.LtdRaffleEntries.Add(
            new LtdRaffleEntryEntity
            {
                SeriesEntityId = (int)this.GetPrimaryKeyLong(),
                PlayerEntityId = playerId,
                BatchId = batchId,
                EnteredAt = DateTime.UtcNow,
                Result = "pending",
            }
        );

        await db.SaveChangesAsync(ct);
    }

    public Task<LtdSeriesSnapshot?> GetSeriesSnapshotAsync(CancellationToken ct) =>
        Task.FromResult(_series);

    public async Task ForceRunRaffleAsync(CancellationToken ct)
    {
        _raffleTimer?.Dispose();
        await ExecuteRaffleAsync();
    }
}
