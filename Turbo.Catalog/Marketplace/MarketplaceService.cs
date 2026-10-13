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
using Turbo.Catalog.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog.Marketplace;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Catalog.Marketplace;

/// <summary>
/// The marketplace (<see cref="IMarketplaceService"/>), kept in <c>marketplace_offers</c>. Posting
/// takes the items out of the seller's inventory (their rows go) and keeps each as an offer row
/// with the item's definition and extra data; taking an offer back or buying it gives a new row
/// of the same back. A purchase claims its row with one conditional update (open and unexpired),
/// so two buyers never get one offer: the later one is refunded and offered the next cheapest of
/// the kind, as Flash's <c>MarketPlaceLogic.onBuyResult</c> expects.
/// </summary>
public sealed class MarketplaceService(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IOptions<CatalogConfig> config,
    IGrainFactory grainFactory,
    IFurnitureDefinitionProvider definitions,
    IInventoryFurnitureLoader furnitureLoader,
    TimeProvider time,
    ILogger<MarketplaceService> logger
) : IMarketplaceService
{
    /// <summary>Days of price history a stats answer covers: 30 in both official captures (2026-10-13).</summary>
    public const int HISTORY_DAYS = 30;

    private readonly MarketplaceConfig _config = config.Value.Marketplace;

    public async Task<MarketplaceOfferPage> SearchOffersAsync(
        int minPrice,
        int maxPrice,
        string query,
        int sortType,
        bool combineUniques,
        CancellationToken ct
    )
    {
        var now = Now();
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var offers = OpenOffers(dbCtx, now);

        if (minPrice >= 0)
            offers = offers.Where(x => x.Price >= minPrice);

        if (maxPrice >= 0)
            offers = offers.Where(x => x.Price <= maxPrice);

        // The server knows a furni by its class name only (the names players read are in the
        // client's furnidata), so a search matches that.
        if (!string.IsNullOrWhiteSpace(query))
            offers = offers.Where(x =>
                EF.Functions.Like(
                    x.FurnitureDefinitionEntity!.Name,
                    $"%{EscapeLike(query.Trim())}%",
                    "\\"
                )
            );

        var rows = await offers.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        var averages = await AveragePricesAsync(dbCtx, now, ct).ConfigureAwait(false);
        var soldToday = await SoldTodayAsync(dbCtx, now, ct).ConfigureAwait(false);

        var groups = rows.Select(row => (Row: row, Offer: Describe(row, now)))
            .Where(x => x.Offer is not null)
            .GroupBy(x => GroupKey(x.Row, x.Offer!, combineUniques))
            .Select(group =>
            {
                var cheapest = group.OrderBy(x => x.Row.Price).ThenBy(x => x.Row.Id).First();

                return (
                    DefinitionId: cheapest.Row.FurnitureDefinitionEntityId,
                    Offer: cheapest.Offer! with
                    {
                        AveragePrice = averages.GetValueOrDefault(
                            cheapest.Row.FurnitureDefinitionEntityId
                        ),
                        OfferCount = group.Count(),
                    }
                );
            })
            .ToList();

        IEnumerable<(int DefinitionId, MarketplaceOfferSnapshot Offer)> sorted = sortType switch
        {
            2 => groups.OrderBy(x => x.Offer.Price),
            3 => groups.OrderByDescending(x => soldToday.GetValueOrDefault(x.DefinitionId)),
            4 => groups.OrderBy(x => soldToday.GetValueOrDefault(x.DefinitionId)),
            5 => groups.OrderByDescending(x => x.Offer.OfferCount),
            6 => groups.OrderBy(x => x.Offer.OfferCount),
            _ => groups.OrderByDescending(x => x.Offer.Price),
        };

        return new MarketplaceOfferPage(
            [.. sorted.Take(Math.Max(0, _config.OfferListLimit)).Select(x => x.Offer)],
            groups.Count
        );
    }

    public async Task<MarketplaceOwnOffers> GetOwnOffersAsync(
        PlayerId player,
        int category,
        CancellationToken ct
    )
    {
        var now = Now();
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var own = dbCtx
            .MarketplaceOffers.AsNoTracking()
            .Where(x => x.PlayerEntityId == player.Value);

        var creditsWaiting = await own.Where(x =>
                x.State == MarketplaceOfferEntity.STATE_SOLD && !x.CreditsRedeemed
            )
            .SumAsync(x => x.Revenue, ct)
            .ConfigureAwait(false);

        var rows = category switch
        {
            (int)MarketplaceOfferStatus.Sold => own.Where(x =>
                x.State == MarketplaceOfferEntity.STATE_SOLD && !x.HistoryCleared
            ),
            (int)MarketplaceOfferStatus.Expired => own.Where(x =>
                x.State == MarketplaceOfferEntity.STATE_OPEN && x.ExpiresAt <= now
            ),
            _ => own.Where(x => x.State == MarketplaceOfferEntity.STATE_OPEN && x.ExpiresAt > now),
        };

        var list = await rows.OrderByDescending(x => x.Id)
            .Take(Math.Max(0, _config.OfferListLimit))
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var averages = await AveragePricesAsync(dbCtx, now, ct).ConfigureAwait(false);

        return new MarketplaceOwnOffers(
            [
                .. list.Select(row =>
                        Describe(row, now) is { } offer
                            ? offer with
                            {
                                AveragePrice = averages.GetValueOrDefault(
                                    row.FurnitureDefinitionEntityId
                                ),
                            }
                            : null
                    )
                    .OfType<MarketplaceOfferSnapshot>(),
            ],
            creditsWaiting
        );
    }

    public async Task<MarketplaceItemStatsSnapshot> GetItemStatsAsync(
        int category,
        int furniTypeId,
        string? extraData,
        CancellationToken ct
    )
    {
        var now = Now();
        var definition = definitions.TryGetDefinitionBySprite(
            category == 2 ? ProductType.Wall : ProductType.Floor,
            furniTypeId
        );

        var empty = new MarketplaceItemStatsSnapshot
        {
            AveragePrice = 0,
            OfferCount = 0,
            HistoryLength = HISTORY_DAYS,
            Days = [],
            FurniCategoryId = category,
            FurniTypeId = furniTypeId,
            LowestCurrentPrice = 0,
            SuggestedPrice = 0,
        };

        if (definition is null)
            return empty;

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var open = (
            await OpenOffers(dbCtx, now)
                .AsNoTracking()
                .Where(x => x.FurnitureDefinitionEntityId == definition.Id)
                .ToListAsync(ct)
                .ConfigureAwait(false)
        )
            .Select(row => Describe(row, now))
            .OfType<MarketplaceOfferSnapshot>()
            .Where(x => category != 3 || x.FurniType == MarketplaceFurniType.Unique)
            .Where(x => string.IsNullOrEmpty(extraData) || x.ExtraData == extraData)
            .ToList();

        var today = Today(now);
        var since = today.AddDays(-HISTORY_DAYS);
        // A poster's history is its own (official capture 18:34: GetMarketplaceItemStats(2, 4001,
        // "2005")), a limited edition's that of the limited ones.
        var sales = (
            await dbCtx
                .MarketplaceOffers.AsNoTracking()
                .Where(x =>
                    x.FurnitureDefinitionEntityId == definition.Id
                    && x.State == MarketplaceOfferEntity.STATE_SOLD
                    && x.SoldAt >= since
                )
                .ToListAsync(ct)
                .ConfigureAwait(false)
        )
            .Where(x =>
                Describe(x, now) is { } sold
                && (category != 3 || sold.FurniType == MarketplaceFurniType.Unique)
                && (string.IsNullOrEmpty(extraData) || sold.ExtraData == extraData)
            )
            .Select(x => new { x.Price, SoldAt = x.SoldAt!.Value })
            .ToList();

        // Days before today with sales, oldest first; today is still trading and not shown
        // (both official captures end at day -1).
        var days = sales
            .Where(x => x.SoldAt < today)
            .GroupBy(x => (int)Math.Floor((x.SoldAt - today).TotalDays))
            .OrderBy(x => x.Key)
            .Select(x => new MarketplaceDayStats
            {
                DayOffset = x.Key,
                AveragePrice = (int)Math.Round(x.Average(y => y.Price)),
                SoldAmount = x.Count(),
            })
            .ToImmutableArray();

        var periodStart = now.AddDays(-_config.AveragePricePeriod);
        var inPeriod = sales.Where(x => x.SoldAt >= periodStart).ToList();
        var average = inPeriod.Count > 0 ? (int)Math.Round(inPeriod.Average(x => x.Price)) : 0;
        var lowest = open.Count > 0 ? open.Min(x => x.Price) : 0;

        return empty with
        {
            AveragePrice = average,
            OfferCount = open.Count,
            Days = days,
            LowestCurrentPrice = lowest,
            // The lowest open price, or the average when nothing is offered: all four official
            // captures (2026-10-13: 15/15, 99999 with no sales, 99 against an average of 568,
            // and 7 for a poster with no offers).
            SuggestedPrice = lowest > 0 ? lowest : average,
        };
    }

    public async Task<MarketplaceCanMakeOffer> CanMakeOfferAsync(
        PlayerId player,
        CancellationToken ct
    )
    {
        var tokens = await TokensAsync(player, ct).ConfigureAwait(false);

        if (!_config.Enabled || !await CanTradeAsync(player, ct).ConfigureAwait(false))
            return new MarketplaceCanMakeOffer(
                MarketplaceCanMakeOfferCode.NoTradingPrivilege,
                tokens
            );

        if (TokensUsed && tokens <= 0)
            return new MarketplaceCanMakeOffer(MarketplaceCanMakeOfferCode.NoTokens, 0);

        return new MarketplaceCanMakeOffer(MarketplaceCanMakeOfferCode.Allowed, tokens);
    }

    public async Task<MarketplaceMakeOfferCode> MakeOfferAsync(
        PlayerId player,
        int price,
        ImmutableArray<int> itemRefs,
        CancellationToken ct
    )
    {
        if (!_config.Enabled)
            return MarketplaceMakeOfferCode.Disabled;

        // A floor item's ref is its id negated, a wall item's its id (FurnitureItemSerializer.WriteHead).
        var itemIds = itemRefs.Select(x => Math.Abs(x)).Where(x => x > 0).Distinct().ToList();

        if (itemIds.Count > _config.MaxItemsPerOffer)
            return MarketplaceMakeOfferCode.TooManyItems;

        if (
            itemIds.Count == 0
            || price < _config.OfferMinPrice
            || price > _config.OfferMaxPrice
            || !await CanTradeAsync(player, ct).ConfigureAwait(false)
        )
            return MarketplaceMakeOfferCode.TechnicalError;

        if (TokensUsed && await TokensAsync(player, ct).ConfigureAwait(false) < itemIds.Count)
            return MarketplaceMakeOfferCode.TechnicalError;

        var inventory = grainFactory.GetInventoryGrain(player);
        var items = await inventory
            .GetItemSnapshotsAsync(
                [.. itemIds.Select(x => (Primitives.Rooms.Object.RoomObjectId)x)],
                ct
            )
            .ConfigureAwait(false);

        // One posting is one kind of furni (Flash offers the items of one inventory group).
        if (
            items.Length != itemIds.Count
            || items.Any(x => !x.Definition.CanSell || x.Definition.Id != items[0].Definition.Id)
        )
            return MarketplaceMakeOfferCode.TechnicalError;

        var now = Now();
        var rows = new List<MarketplaceOfferEntity>(items.Length);

        foreach (var item in items)
        {
            var taken = await inventory
                .ConsumeFurnitureAsync(item.ItemId, ct)
                .ConfigureAwait(false);

            if (taken is null)
                continue;

            rows.Add(
                new MarketplaceOfferEntity
                {
                    PlayerEntityId = player.Value,
                    FurnitureDefinitionEntityId = taken.Definition.Id,
                    ExtraData = taken.ExtraData,
                    Price = price,
                    State = MarketplaceOfferEntity.STATE_OPEN,
                    ExpiresAt = now.AddHours(_config.ExpirationHours),
                    Revenue = Revenue(price),
                }
            );
        }

        if (rows.Count == 0)
            return MarketplaceMakeOfferCode.TechnicalError;

        try
        {
            var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
            await using var dbCtxScope = dbCtx.ConfigureAwait(false);

            dbCtx.MarketplaceOffers.AddRange(rows);
            await dbCtx.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);

            if (TokensUsed)
                await dbCtx
                    .MarketplaceTokens.Where(x => x.PlayerEntityId == player.Value)
                    .ExecuteUpdateAsync(
                        u => u.SetProperty(x => x.Tokens, x => x.Tokens - rows.Count),
                        CancellationToken.None
                    )
                    .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Marketplace offers of player {PlayerId} could not be saved; giving the {Count} items back",
                player.Value,
                rows.Count
            );

            foreach (var row in rows)
                await GiveItemAsync(player, row).ConfigureAwait(false);

            return MarketplaceMakeOfferCode.TechnicalError;
        }

        return MarketplaceMakeOfferCode.Success;
    }

    public async Task<bool> BuyTokensAsync(PlayerId player, CancellationToken ct)
    {
        if (!_config.Enabled || !TokensUsed)
            return false;

        var debits = new List<WalletDebitRequest>();

        if (_config.TokenBatchPrice > 0)
            debits.Add(
                new WalletDebitRequest
                {
                    CurrencyKind = CurrencyKind.Credits,
                    Amount = _config.TokenBatchPrice,
                }
            );

        if (
            debits.Count > 0
            && !(
                await grainFactory
                    .GetPlayerWalletGrain(player)
                    .TryDebitAsync(debits, ct)
                    .ConfigureAwait(false)
            ).Succeeded
        )
            return false;

        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var updated = await dbCtx
            .MarketplaceTokens.Where(x => x.PlayerEntityId == player.Value)
            .ExecuteUpdateAsync(
                u => u.SetProperty(x => x.Tokens, x => x.Tokens + _config.TokenBatchSize),
                CancellationToken.None
            )
            .ConfigureAwait(false);

        if (updated == 0)
        {
            dbCtx.MarketplaceTokens.Add(
                new MarketplaceTokenEntity
                {
                    PlayerEntityId = player.Value,
                    Tokens = _config.TokenBatchSize,
                }
            );
            await dbCtx.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }

        return true;
    }

    public async Task<bool> CancelOfferAsync(PlayerId player, int offerId, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .MarketplaceOffers.AsNoTracking()
            .FirstOrDefaultAsync(
                x =>
                    x.Id == offerId
                    && x.PlayerEntityId == player.Value
                    && x.State == MarketplaceOfferEntity.STATE_OPEN,
                ct
            )
            .ConfigureAwait(false);

        if (row is null)
            return false;

        // The delete is the claim: a buyer who got there first leaves nothing to delete.
        var deleted = await dbCtx
            .MarketplaceOffers.Where(x =>
                x.Id == offerId && x.State == MarketplaceOfferEntity.STATE_OPEN
            )
            .ExecuteDeleteAsync(CancellationToken.None)
            .ConfigureAwait(false);

        if (deleted == 0)
            return false;

        await GiveItemAsync(player, row).ConfigureAwait(false);

        return true;
    }

    public async Task<MarketplaceBuyResult> BuyOfferAsync(
        PlayerId player,
        int offerId,
        CancellationToken ct
    )
    {
        var now = Now();
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var row = await dbCtx
            .MarketplaceOffers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == offerId, ct)
            .ConfigureAwait(false);

        if (!_config.Enabled || row is null)
            return new MarketplaceBuyResult(MarketplaceBuyResultCode.SoldOut, 0, 0, offerId);

        if (row.State != MarketplaceOfferEntity.STATE_OPEN || row.ExpiresAt <= now)
            return await NextOfferAsync(dbCtx, row, now, ct).ConfigureAwait(false);

        var debit = new WalletDebitRequest
        {
            CurrencyKind = CurrencyKind.Credits,
            Amount = row.Price,
        };
        var paid = await grainFactory
            .GetPlayerWalletGrain(player)
            .TryDebitAsync([debit], ct)
            .ConfigureAwait(false);

        if (!paid.Succeeded)
            return new MarketplaceBuyResult(
                MarketplaceBuyResultCode.NotEnoughCredits,
                0,
                0,
                offerId
            );

        var claimed = await dbCtx
            .MarketplaceOffers.Where(x =>
                x.Id == offerId && x.State == MarketplaceOfferEntity.STATE_OPEN && x.ExpiresAt > now
            )
            .ExecuteUpdateAsync(
                u =>
                    u.SetProperty(x => x.State, MarketplaceOfferEntity.STATE_SOLD)
                        .SetProperty(x => x.BuyerPlayerEntityId, player.Value)
                        .SetProperty(x => x.SoldAt, now)
                        .SetProperty(x => x.Revenue, Revenue(row.Price)),
                CancellationToken.None
            )
            .ConfigureAwait(false);

        if (claimed == 0)
        {
            await grainFactory
                .RefundAsync(player, [debit], logger, $"marketplace offer {offerId}")
                .ConfigureAwait(false);

            return await NextOfferAsync(dbCtx, row, now, ct).ConfigureAwait(false);
        }

        await GiveItemAsync(player, row).ConfigureAwait(false);

        return new MarketplaceBuyResult(
            MarketplaceBuyResultCode.Bought,
            offerId,
            row.Price,
            offerId
        );
    }

    public async Task<ImmutableArray<int>> CancelAllOffersAsync(
        PlayerId player,
        CancellationToken ct
    )
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var offerIds = await dbCtx
            .MarketplaceOffers.AsNoTracking()
            .Where(x =>
                x.PlayerEntityId == player.Value && x.State == MarketplaceOfferEntity.STATE_OPEN
            )
            .Select(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var taken = ImmutableArray.CreateBuilder<int>(offerIds.Count);

        foreach (var offerId in offerIds)
            if (await CancelOfferAsync(player, offerId, ct).ConfigureAwait(false))
                taken.Add(offerId);

        return taken.ToImmutable();
    }

    public async Task<bool> ClearOwnHistoryAsync(
        PlayerId player,
        int category,
        CancellationToken ct
    )
    {
        var now = Now();
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        switch (category)
        {
            case (int)MarketplaceOfferStatus.Sold:
                // The rows stay for the price history and the credits still to be paid.
                await dbCtx
                    .MarketplaceOffers.Where(x =>
                        x.PlayerEntityId == player.Value
                        && x.State == MarketplaceOfferEntity.STATE_SOLD
                    )
                    .ExecuteUpdateAsync(
                        u => u.SetProperty(x => x.HistoryCleared, true),
                        CancellationToken.None
                    )
                    .ConfigureAwait(false);

                return true;
            case (int)MarketplaceOfferStatus.Expired:
                // Inference: an expired offer still holds its item, so clearing one takes the
                // item back rather than losing it.
                var expired = await dbCtx
                    .MarketplaceOffers.AsNoTracking()
                    .Where(x =>
                        x.PlayerEntityId == player.Value
                        && x.State == MarketplaceOfferEntity.STATE_OPEN
                        && x.ExpiresAt <= now
                    )
                    .Select(x => x.Id)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                foreach (var offerId in expired)
                    await CancelOfferAsync(player, offerId, ct).ConfigureAwait(false);

                return true;
            default:
                return false;
        }
    }

    public async Task<int> RedeemCreditsAsync(PlayerId player, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        var sold = await dbCtx
            .MarketplaceOffers.AsNoTracking()
            .Where(x =>
                x.PlayerEntityId == player.Value
                && x.State == MarketplaceOfferEntity.STATE_SOLD
                && !x.CreditsRedeemed
            )
            .Select(x => new { x.Id, x.Revenue })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var total = 0;

        // Row by row, each marked before it pays: a second redeem at the same moment finds it taken.
        foreach (var offer in sold)
        {
            var marked = await dbCtx
                .MarketplaceOffers.Where(x => x.Id == offer.Id && !x.CreditsRedeemed)
                .ExecuteUpdateAsync(
                    u => u.SetProperty(x => x.CreditsRedeemed, true),
                    CancellationToken.None
                )
                .ConfigureAwait(false);

            if (marked > 0)
                total += offer.Revenue;
        }

        if (total > 0)
            await grainFactory
                .GetPlayerWalletGrain(player)
                .CreditAsync(CurrencyKind.Credits, total, CancellationToken.None)
                .ConfigureAwait(false);

        return total;
    }

    /// <summary>
    /// What the seller receives for a sale at <paramref name="price"/>: Flash's
    /// <c>MarketplaceView.calculateFinalPrice</c> (inventory/marketplace/MarketplaceView.as:325),
    /// the price less a fee of the selling percentage plus half the price's share of the half tax limit
    /// (Flash's Math.round rounds halves up).
    /// </summary>
    public int Revenue(int price)
    {
        var halfTax = _config.HalfTaxLimit > 0 ? 0.5 * price / _config.HalfTaxLimit : 0;
        var fee = (int)
            Math.Ceiling(
                Math.Round(
                    1000 * (price * (_config.SellingFeePercentage / 100.0 + halfTax)),
                    MidpointRounding.AwayFromZero
                ) / 1000
            );

        return Math.Max(0, price - fee);
    }

    private bool TokensUsed => _config.TokenBatchSize > 0;

    private DateTime Now() => time.GetUtcNow().UtcDateTime;

    private static DateTime Today(DateTime now) => now.Date;

    private static IQueryable<MarketplaceOfferEntity> OpenOffers(
        TurboDbContext dbCtx,
        DateTime now
    ) =>
        dbCtx.MarketplaceOffers.Where(x =>
            x.State == MarketplaceOfferEntity.STATE_OPEN && x.ExpiresAt > now
        );

    private async Task<bool> CanTradeAsync(PlayerId player, CancellationToken ct) =>
        await grainFactory
            .HasPermissionAsync(player, PermissionNodes.TRADE, ct)
            .ConfigureAwait(false);

    private async Task<int> TokensAsync(PlayerId player, CancellationToken ct)
    {
        var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbCtxScope = dbCtx.ConfigureAwait(false);

        return await dbCtx
            .MarketplaceTokens.Where(x => x.PlayerEntityId == player.Value)
            .Select(x => x.Tokens)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>The average sale price per definition over the configured period.</summary>
    private async Task<Dictionary<int, int>> AveragePricesAsync(
        TurboDbContext dbCtx,
        DateTime now,
        CancellationToken ct
    )
    {
        var since = now.AddDays(-_config.AveragePricePeriod);
        var averages = await dbCtx
            .MarketplaceOffers.AsNoTracking()
            .Where(x => x.State == MarketplaceOfferEntity.STATE_SOLD && x.SoldAt >= since)
            .GroupBy(x => x.FurnitureDefinitionEntityId)
            .Select(x => new { Id = x.Key, Average = x.Average(y => (double)y.Price) })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return averages.ToDictionary(x => x.Id, x => (int)Math.Round(x.Average));
    }

    /// <summary>Sales per definition since midnight (UTC): what sorts 3 and 4 ("traded today") order by.</summary>
    private static async Task<Dictionary<int, int>> SoldTodayAsync(
        TurboDbContext dbCtx,
        DateTime now,
        CancellationToken ct
    )
    {
        var today = Today(now);
        var counts = await dbCtx
            .MarketplaceOffers.AsNoTracking()
            .Where(x => x.State == MarketplaceOfferEntity.STATE_SOLD && x.SoldAt >= today)
            .GroupBy(x => x.FurnitureDefinitionEntityId)
            .Select(x => new { Id = x.Key, Count = x.Count() })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return counts.ToDictionary(x => x.Id, x => x.Count);
    }

    /// <summary>
    /// After a lost purchase: the cheapest other open offer of the same kind at its price
    /// (<c>catalog.marketplace.confirm_higher_*</c>), or sold out when there is none.
    /// </summary>
    private async Task<MarketplaceBuyResult> NextOfferAsync(
        TurboDbContext dbCtx,
        MarketplaceOfferEntity lost,
        DateTime now,
        CancellationToken ct
    )
    {
        var lostOffer = Describe(lost, now);
        var candidates = await OpenOffers(dbCtx, now)
            .AsNoTracking()
            .Where(x =>
                x.FurnitureDefinitionEntityId == lost.FurnitureDefinitionEntityId && x.Id != lost.Id
            )
            .OrderBy(x => x.Price)
            .ThenBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var next = candidates.FirstOrDefault(x =>
            lostOffer is null || Describe(x, now)?.ExtraData == lostOffer.ExtraData
        );

        return next is null
            ? new MarketplaceBuyResult(MarketplaceBuyResultCode.SoldOut, 0, 0, lost.Id)
            : new MarketplaceBuyResult(
                MarketplaceBuyResultCode.PriceChanged,
                next.Id,
                next.Price,
                lost.Id
            );
    }

    private async Task GiveItemAsync(PlayerId player, MarketplaceOfferEntity row)
    {
        var given = await grainFactory
            .GetInventoryGrain(player)
            .GrantFurnitureAsync(
                row.FurnitureDefinitionEntityId,
                row.ExtraData,
                CancellationToken.None
            )
            .ConfigureAwait(false);

        if (given is null)
            logger.LogError(
                "Marketplace offer {OfferId}: definition {DefinitionId} is missing, player {PlayerId} got nothing",
                row.Id,
                row.FurnitureDefinitionEntityId,
                player.Value
            );
    }

    /// <summary>A row as the client draws it, or null when its definition is gone.</summary>
    private MarketplaceOfferSnapshot? Describe(MarketplaceOfferEntity row, DateTime now)
    {
        var definition = definitions.TryGetDefinition(row.FurnitureDefinitionEntityId);

        if (definition is null)
            return null;

        var stuff = furnitureLoader
            .Create(row.Id, row.PlayerEntityId, string.Empty, definition, row.ExtraData, null)
            .GetSnapshot()
            .StuffData;
        var isWall = definition.ProductType == ProductType.Wall;
        var expired = row.State == MarketplaceOfferEntity.STATE_OPEN && row.ExpiresAt <= now;

        return new MarketplaceOfferSnapshot
        {
            OfferId = row.Id,
            FurniTypeId = definition.SpriteId,
            FurniType =
                isWall ? MarketplaceFurniType.Wall
                : stuff?.UniqueNumber > 0 ? MarketplaceFurniType.Unique
                : MarketplaceFurniType.Floor,
            ExtraData = isWall
                ? (stuff as LegacyStuffSnapshot)?.Data ?? string.Empty
                : string.Empty,
            StuffData = isWall ? null : stuff,
            Price = row.Price,
            Status =
                row.State == MarketplaceOfferEntity.STATE_SOLD ? MarketplaceOfferStatus.Sold
                : expired ? MarketplaceOfferStatus.Expired
                : MarketplaceOfferStatus.Open,
            TimeLeftMinutes =
                expired || row.State != MarketplaceOfferEntity.STATE_OPEN
                    ? 0
                    : (int)Math.Ceiling((row.ExpiresAt - now).TotalMinutes),
            AveragePrice = 0,
            StatusTime =
                row.State == MarketplaceOfferEntity.STATE_SOLD
                    ? new DateTimeOffset(
                        DateTime.SpecifyKind(row.SoldAt ?? now, DateTimeKind.Utc)
                    ).ToUnixTimeMilliseconds()
                    : new DateTimeOffset(
                        DateTime.SpecifyKind(row.ExpiresAt, DateTimeKind.Utc)
                    ).ToUnixTimeMilliseconds(),
        };
    }

    /// <summary>
    /// What one public row stands for: a definition, a wall item's data (each poster its own row),
    /// and a limited edition's own offer unless the search combines them.
    /// </summary>
    private static string GroupKey(
        MarketplaceOfferEntity row,
        MarketplaceOfferSnapshot offer,
        bool combineUniques
    ) =>
        offer.FurniType switch
        {
            MarketplaceFurniType.Wall => $"{row.FurnitureDefinitionEntityId}:w:{offer.ExtraData}",
            MarketplaceFurniType.Unique when !combineUniques =>
                $"{row.FurnitureDefinitionEntityId}:u:{row.Id}",
            MarketplaceFurniType.Unique => $"{row.FurnitureDefinitionEntityId}:u",
            _ => $"{row.FurnitureDefinitionEntityId}",
        };

    private static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
