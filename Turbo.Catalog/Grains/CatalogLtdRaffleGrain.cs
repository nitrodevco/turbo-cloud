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
        _state = new() { SeriesId = this.GetLtdSeriesId() };
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

        if (
            !snap.TryGetLtdProduct(_state.Series.Id, out var product)
            || !snap.OffersById.TryGetValue(product.OfferId, out var offer)
        )
            return LtdRaffleEntryResult.Failed(LtdRaffleEntryErrorType.None);

        // A hidden offer is not sold, limited or not.
        if (!offer.Visible)
            return LtdRaffleEntryResult.Failed(LtdRaffleEntryErrorType.SeriesNotFound);

        // The same club rule as a shop purchase: an LTD is an offer like any other.
        if (offer.RequiresClub && !await _grainFactory.HasActiveClubAsync(playerId, ct))
            return LtdRaffleEntryResult.Failed(LtdRaffleEntryErrorType.RequiresHabboClub);

        // Three reads of one wallet grain: sent together, they queue there instead of each
        // waiting for the previous round trip.
        var walletGrain = _grainFactory.GetPlayerWalletGrain(playerId);
        var creditsTask = walletGrain.GetAmountForCurrencyAsync(CurrencyKind.Credits, ct);
        var silverTask =
            offer.CostSilver > 0
                ? walletGrain.GetAmountForCurrencyAsync(CurrencyKind.Silver, ct)
                : Task.FromResult(0);
        var activityPointsTask = walletGrain.GetActivityPointsAsync(ct);

        await Task.WhenAll(creditsTask, silverTask, activityPointsTask);

        var credits = await creditsTask;
        var silver = await silverTask;
        var activityPoints = await activityPointsTask;

        // The client has no "not enough silver" alert; the balance packet's credits flag is the
        // nearest it draws, and the charge at the draw refuses a short silver balance anyway.
        var hasInsufficientCredits = offer.CostCredits > credits || offer.CostSilver > silver;
        var hasInsufficientActivityPoints =
            offer is { CostCurrency: > 0, ActivityPointType: not null }
            && activityPoints.GetValueOrDefault(offer.ActivityPointType.Value) < offer.CostCurrency;

        if (hasInsufficientCredits || hasInsufficientActivityPoints)
        {
            return LtdRaffleEntryResult.Failed(
                LtdRaffleEntryErrorType.InsufficientFunds,
                new CatalogBalanceFailure
                {
                    NotEnoughCredits = hasInsufficientCredits,
                    NotEnoughActivityPoints = hasInsufficientActivityPoints,
                    ActivityPointType = offer.ActivityPointType ?? 0,
                }
            );
        }

        var window = WindowLeft(_state.Series, DateTime.UtcNow);

        // One per customer counts what they bought outright as well as what they won.
        if (_config.LtdRaffle.LimitOnePerCustomer)
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var alreadyWon = await dbCtx.LtdRaffleEntries.AnyAsync(
                e =>
                    e.SeriesEntityId == _state.Series.Id
                    && e.PlayerEntityId == playerId.Value
                    && e.Result == LtdEntryResult.Won,
                ct
            );

            if (alreadyWon)
                return LtdRaffleEntryResult.Failed(LtdRaffleEntryErrorType.AlreadyWon);
        }

        if (window is null || _state.RaffleFinished)
        {
            return await TryFinalizeWinnerAsync(playerId, null, false, ct) switch
            {
                LtdRaffleResultType.Won => LtdRaffleEntryResult.Succeeded("instant"),
                LtdRaffleResultType.LostNoStock => LtdRaffleEntryResult.Failed(
                    LtdRaffleEntryErrorType.SoldOut
                ),
                LtdRaffleResultType.Lost => LtdRaffleEntryResult.Failed(
                    LtdRaffleEntryErrorType.InsufficientFunds
                ),
                _ => LtdRaffleEntryResult.Failed(LtdRaffleEntryErrorType.None),
            };
        }

        if (_state.CurrentBatchEntries.ContainsKey(playerId))
            return LtdRaffleEntryResult.Failed(LtdRaffleEntryErrorType.AlreadyInQueue);

        if (_state.CurrentBatchId == null)
        {
            _state.CurrentBatchId = Guid.CreateVersion7();
            _state.IsInBufferPeriod = true;
            _raffleTimer = this.RegisterGrainTimer<object?>(
                static async (self, ct) =>
                    await ((CatalogLtdRaffleGrain)self!).ExecuteRaffleAsync(ct),
                this,
                window.Value,
                Timeout.InfiniteTimeSpan
            );
        }

        if (!_state.IsInBufferPeriod)
            return LtdRaffleEntryResult.Failed(LtdRaffleEntryErrorType.RaffleProcessing);

        if (_state.CurrentBatchEntries.Count >= _config.LtdRaffle.MaxEntriesPerBatch)
            return LtdRaffleEntryResult.Failed(LtdRaffleEntryErrorType.RaffleProcessing);

        _state.CurrentBatchEntries[playerId] = await CalculateWeightAsync(playerId, ct);
        await PersistEntryAsync(playerId, _state.CurrentBatchId.Value, ct);

        await _grainFactory.SendComposerToPlayerAsync(
            playerId,
            new LtdRaffleEnteredMessageComposer { ClassName = product.ClassName ?? "LTD" },
            CancellationToken.None
        );

        return LtdRaffleEntryResult.Succeeded(_state.CurrentBatchId.Value.ToString());
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

        // Disposing a grain timer cancels the token it handed its callback, which is this draw's
        // when the timer runs it: disposed first, the draw's next query was cancelled after the
        // raffle was marked finished, so nobody won and every entry stayed pending.
        var timer = _raffleTimer;
        _raffleTimer = null;

        try
        {
            await DrawAsync(batchId, entries, ct);
        }
        finally
        {
            timer?.Dispose();
        }
    }

    private async Task DrawAsync(
        Guid? batchId,
        List<KeyValuePair<int, double>> entries,
        CancellationToken ct
    )
    {
        await PersistFinishedAsync();
        await ReloadSeriesAsync(ct);

        // As Habbo draws: everyone who entered is put in order, weighted towards experienced
        // players unless UsePureRandom, and the stock is sold down that order. One who cannot pay
        // now is passed over for the next, so no piece goes unsold while an entrant is left.
        // Every entrant is told how it went, and every entry is settled.
        var remaining = _state.Series?.RemainingQuantity ?? 0;
        var results = new Dictionary<int, LtdRaffleResultType>();

        // One sale at a time: each takes a piece of stock and a serial.
        foreach (var playerId in DrawOrder(entries))
        {
            if (remaining <= 0)
            {
                results[playerId] = LtdRaffleResultType.Lost;
                continue;
            }

            var result = await TryFinalizeWinnerAsync(playerId, batchId, true, ct);

            results[playerId] = result;

            if (result == LtdRaffleResultType.Won)
                remaining--;
            else if (result == LtdRaffleResultType.LostNoStock)
                remaining = 0;
        }

        var losers = results.Where(x => x.Value != LtdRaffleResultType.Won).ToList();

        if (losers.Count > 0)
        {
            // Different presence grains, so side by side.
            await Task.WhenAll(losers.Select(x => NotifyLoserAsync(x.Key, x.Value, ct)));

            var loserIds = losers.Select(x => x.Key).ToList();
            await using var db = await _dbCtxFactory.CreateDbContextAsync(CancellationToken.None);

            await db
                .LtdRaffleEntries.Where(e =>
                    e.BatchId == batchId && loserIds.Contains(e.PlayerEntityId)
                )
                .ExecuteUpdateAsync(u =>
                    u.SetProperty(e => e.Result, LtdEntryResult.Lost)
                        .SetProperty(e => e.ProcessedAt, DateTime.UtcNow)
                );
        }

        await ReloadSeriesAsync(CancellationToken.None);
    }

    /// <summary>
    /// How long the raffle that would open now gathers buyers, or null when the series is sold
    /// first come, first served (a window of 0). Habbo opens the window with the sale: when the
    /// series has a start time and that window is still running, it closes on time whoever
    /// comes first; otherwise the first buyer opens it.
    /// </summary>
    internal static TimeSpan? WindowLeft(LtdSeriesSnapshot series, DateTime nowUtc)
    {
        if (series.RaffleWindowSeconds <= 0)
            return null;

        var length = TimeSpan.FromSeconds(series.RaffleWindowSeconds);

        if (series.StartsAt is { } start && nowUtc >= start && nowUtc < start + length)
            return start + length - nowUtc;

        return length;
    }

    /// <summary>The order the stock is offered in: weighted, or pure random.</summary>
    private List<int> DrawOrder(List<KeyValuePair<int, double>> entries) =>
        _config.LtdRaffle.UsePureRandom
            ? [.. entries.Select(e => e.Key).OrderBy(_ => Random.Shared.Next())]
            : SelectWeighted(entries, entries.Count);

    /// <summary>
    /// Sells one piece to the player: charges them, takes it from the stock with its serial,
    /// then gives it. Won, or why not: none left, they could not pay, or something failed (and
    /// whatever was charged is given back).
    /// </summary>
    private async Task<LtdRaffleResultType> TryFinalizeWinnerAsync(
        int playerId,
        Guid? batchId,
        bool isRaffle,
        CancellationToken ct
    )
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);
        await using var tx = await dbCtx.Database.BeginTransactionAsync(ct);

        // The wallet is another grain, so the charge cannot join the transaction. If anything
        // between the charge and the commit fails, the rollback undoes the serial and the charge
        // is given back by hand.
        List<WalletDebitRequest>? charged = null;
        var committed = false;

        try
        {
            var series = await dbCtx
                .LtdSeries.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == _state.SeriesId);

            if (series is not { RemainingQuantity: > 0 })
                return LtdRaffleResultType.LostNoStock;

            var snap = _catalogService.GetCatalogSnapshot(CatalogType.Normal);

            // Nothing is charged yet, so a series the catalog no longer sells is a plain refusal.
            if (
                !snap.TryGetLtdProduct(series.Id, out var prod)
                || !snap.OffersById.TryGetValue(prod.OfferId, out var offer)
            )
            {
                _logger.LogWarning(
                    "No catalog offer sells LTD series {SeriesId}; player {PlayerId} was not given one",
                    series.Id,
                    playerId
                );

                return LtdRaffleResultType.LostError;
            }

            var debits = offer.ToDebitRequests(1);
            var debitResult = await _grainFactory
                .GetPlayerWalletGrain(playerId)
                .TryDebitAsync(
                    debits,
                    new WalletChange(
                        WalletChangeReason.CatalogPurchase,
                        $"LTD series {series.Id}, offer {offer.Id}"
                    ),
                    ct
                );

            if (!debitResult.Succeeded)
                return LtdRaffleResultType.Lost;

            charged = debits;

            var serial = _config.LtdRaffle.RandomizeSerials
                ? await PickAvailableSerialAsync(dbCtx, series)
                : (series.TotalQuantity - series.RemainingQuantity) + 1;

            // One taken from the stock only while there is one: the grain sells a series one
            // buyer at a time, and this holds even if two servers ever did. The serial's unique
            // index refuses one sold twice.
            var taken = await dbCtx
                .LtdSeries.Where(x => x.Id == series.Id && x.RemainingQuantity > 0)
                .ExecuteUpdateAsync(
                    u => u.SetProperty(x => x.RemainingQuantity, x => x.RemainingQuantity - 1),
                    ct
                );

            if (taken != 1)
                throw new InvalidOperationException("The series sold out during the sale.");

            // The serial is recorded whether it was drawn or sold outright, so it is never
            // drawn or sold again.
            var entry = batchId is null
                ? null
                : await dbCtx
                    .LtdRaffleEntries.OrderBy(e => e.Id)
                    .FirstOrDefaultAsync(e => e.BatchId == batchId && e.PlayerEntityId == playerId);

            if (entry is null)
            {
                entry = new LtdRaffleEntryEntity
                {
                    SeriesEntityId = series.Id,
                    PlayerEntityId = playerId,
                    BatchId = batchId,
                    Result = LtdEntryResult.Won,
                };
                dbCtx.LtdRaffleEntries.Add(entry);
            }

            entry.Result = LtdEntryResult.Won;
            entry.SerialNumber = serial;
            entry.ProcessedAt = DateTime.UtcNow;

            await dbCtx.SaveChangesAsync();
            await tx.CommitAsync();

            committed = true;

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

            return LtdRaffleResultType.Won;
        }
        catch (Exception ex) when (committed)
        {
            // The sale stands: paid for, its serial theirs. Rolling back a committed transaction
            // only threw again and stopped the rest of the draw.
            _logger.LogError(
                ex,
                "Sold LTD series {SeriesId} to player {PlayerId}, but giving it to them failed",
                _state.SeriesId,
                playerId
            );

            return LtdRaffleResultType.Won;
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

            if (charged is not null)
                await _grainFactory.RefundAsync(
                    PlayerId.Parse(playerId),
                    charged,
                    _logger,
                    $"LTD series {_state.SeriesId}"
                );

            return LtdRaffleResultType.LostError;
        }
    }

    private static async Task<int> PickAvailableSerialAsync(TurboDbContext db, LtdSeriesEntity s)
    {
        var available = await GetAvailableSerialsAsync(db, s);

        if (available.Count == 0)
            throw new InvalidOperationException("Every serial of the series is sold.");

        return available[Random.Shared.Next(available.Count)];
    }

    private static async Task<List<int>> GetAvailableSerialsAsync(
        TurboDbContext db,
        LtdSeriesEntity s
    )
    {
        var usedSerials = await db
            .LtdRaffleEntries.Where(e => e.SeriesEntityId == s.Id && e.SerialNumber != null)
            .Select(e => e.SerialNumber!.Value)
            .ToListAsync();

        return [.. Enumerable.Range(1, s.TotalQuantity).Except(usedSerials)];
    }

    private async Task<double> CalculateWeightAsync(int playerId, CancellationToken ct)
    {
        var cfg = _config.LtdRaffle;
        var playerGrain = _grainFactory.GetPlayerGrain(PlayerId.Parse(playerId));

        // The inputs come from the player grain, the messenger and the database, none waiting
        // on another, so they are read side by side.
        var summaryTask = playerGrain.GetSummaryAsync(ct);
        var profileTask = playerGrain.GetExtendedProfileSnapshotAsync(ct);
        var friendCountTask = cfg.FriendCount.Enabled
            ? _grainFactory
                .GetPlayerMessengerGrain(PlayerId.Parse(playerId))
                .GetFriendCountAsync(ct)
            : Task.FromResult(0);
        var dbWeightTask = CalculateDatabaseWeightAsync(playerId, ct);

        await Task.WhenAll(summaryTask, profileTask, friendCountTask, dbWeightTask);

        var summary = await summaryTask;
        var profile = await profileTask;
        var weight = cfg.BaseWeight + await dbWeightTask;

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
                await friendCountTask * cfg.FriendCount.BonusPerUnit,
                cfg.FriendCount.MaxBonus
            );

        if (cfg.RespectsReceived.Enabled)
            weight += Math.Min(
                profile.StarGemCount * cfg.RespectsReceived.BonusPerUnit,
                cfg.RespectsReceived.MaxBonus
            );

        return weight;
    }

    /// <summary>
    /// The weight counted from rows: badges, rooms and furniture owned. One context, so the
    /// counts run one after another; together they still run beside the grain reads.
    /// </summary>
    private async Task<double> CalculateDatabaseWeightAsync(int playerId, CancellationToken ct)
    {
        // TODO: Replace these with snapshot-based lookups once PlayerGrain exposes badge count,
        // room count and furniture count in PlayerSummarySnapshot or a dedicated snapshot.
        var cfg = _config.LtdRaffle;

        if (!cfg.BadgeCount.Enabled && !cfg.RoomCount.Enabled && !cfg.FurnitureCount.Enabled)
            return 0;

        var weight = 0.0;

        await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);

        if (cfg.BadgeCount.Enabled)
        {
            var badgeCount = await db.PlayerBadges.CountAsync(
                b => b.PlayerEntityId == playerId,
                ct
            );
            weight += Math.Min(badgeCount * cfg.BadgeCount.BonusPerUnit, cfg.BadgeCount.MaxBonus);
        }

        if (cfg.RoomCount.Enabled)
        {
            var roomCount = await db.Rooms.CountAsync(r => r.PlayerEntityId == playerId, ct);
            weight += Math.Min(roomCount * cfg.RoomCount.BonusPerUnit, cfg.RoomCount.MaxBonus);
        }

        if (cfg.FurnitureCount.Enabled)
        {
            var furniCount = await db.Furnitures.CountAsync(f => f.PlayerEntityId == playerId, ct);
            weight += Math.Min(
                furniCount * cfg.FurnitureCount.BonusPerUnit,
                cfg.FurnitureCount.MaxBonus
            );
        }

        return weight;
    }

    /// <summary>Picks <paramref name="count"/> entrants in turn, each in proportion to their weight.</summary>
    private static List<int> SelectWeighted(List<KeyValuePair<int, double>> entries, int count)
    {
        var winners = new List<int>();
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
        var className =
            _state.Series is { } series
            && _catalogService
                .GetCatalogSnapshot(CatalogType.Normal)
                .TryGetLtdProduct(series.Id, out var product)
                ? product.ClassName
                : null;

        await _grainFactory.SendComposerToPlayerAsync(
            playerId,
            new LtdRaffleResultMessageComposer
            {
                ClassName = className ?? "LTD",
                ResultCode = resultCode,
            },
            ct
        );
    }

    private async Task PersistEntryAsync(int playerId, Guid batchId, CancellationToken ct)
    {
        await using var db = await _dbCtxFactory.CreateDbContextAsync(ct);

        db.LtdRaffleEntries.Add(
            new LtdRaffleEntryEntity
            {
                SeriesEntityId = _state.SeriesId,
                PlayerEntityId = playerId,
                BatchId = batchId,
                Result = LtdEntryResult.Pending,
            }
        );

        await db.SaveChangesAsync(ct);
    }

    public Task<LtdSeriesSnapshot?> GetSeriesSnapshotAsync(CancellationToken ct) =>
        Task.FromResult(_state.Series);

    public Task ForceRunRaffleAsync(CancellationToken ct) => ExecuteRaffleAsync(ct);
}
