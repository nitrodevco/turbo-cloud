using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Catalog.Exceptions;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Catalog.Grains;

/// <summary>
/// Habbo Club gifts: a member earns one for every period of membership they use up and picks it
/// off a shelf of catalog offers. Claiming runs here rather than in a handler because this grain
/// is one per player and takes one call at a time, which is what keeps two clicks from spending
/// the same gift twice.
/// </summary>
internal sealed partial class CatalogPurchaseGrain
{
    public async Task<ClubGiftInfoSnapshot> GetClubGiftInfoAsync(CancellationToken ct)
    {
        var offers = _catalogService.GetClubGiftOffers();

        // A hotel that puts nothing on the shelf is the common case, and answering it costs no
        // query at all: this is asked on every login.
        if (offers.Length == 0)
            return ClubGiftInfoSnapshot.Empty;

        var playerId = this.GetPlayerId();

        var club = await _grainFactory
            .GetPlayerSubscriptionGrain(playerId)
            .GetAsync(SubscriptionType.HabboClub, ct);

        var claimed = await CountClaimedGiftsAsync(ct);
        var earned = club.DaysConsumed / _giftIntervalDays;
        var available = Math.Max(0, earned - claimed);

        return new ClubGiftInfoSnapshot
        {
            // Only a member is working towards the next gift. The client reads any countdown
            // above zero as "your next gift is N days away", so telling somebody who holds no
            // membership that they are 31 days from a gift would be a promise of nothing; zero
            // sends it to the "no club" text instead.
            DaysUntilNextGift = club.IsActive
                ? _giftIntervalDays - (club.DaysConsumed % _giftIntervalDays)
                : 0,
            GiftsAvailable = available,
            Offers = offers,
            Gifts =
            [
                .. offers.Select(offer => new ClubGiftSnapshot
                {
                    OfferId = offer.Id,
                    IsVip = false,
                    DaysRequired = offer.ClubGiftDaysRequired ?? 0,
                    IsSelectable =
                        available > 0 && club.DaysConsumed >= (offer.ClubGiftDaysRequired ?? 0),
                }),
            ],
        };
    }

    public async Task<CatalogOfferSnapshot> ClaimClubGiftAsync(
        string productCode,
        CancellationToken ct
    )
    {
        var playerId = this.GetPlayerId();

        var offer = _catalogService
            .GetClubGiftOffers()
            .FirstOrDefault(x =>
                string.Equals(x.LocalizationId, productCode, StringComparison.Ordinal)
            );

        if (offer is null)
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.OfferNotFound);

        // Everything the client was told is checked again here. The shelf it drew came from
        // GetClubGiftInfoAsync, but nothing stops a client sending a product code it was never
        // offered, or clicking twice while the first claim is still in flight.
        var club = await _grainFactory
            .GetPlayerSubscriptionGrain(playerId)
            .GetAsync(SubscriptionType.HabboClub, ct);

        if (club.DaysConsumed < (offer.ClubGiftDaysRequired ?? 0))
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.RequiresHabboClub);

        var claimed = await CountClaimedGiftsAsync(ct);

        if (club.DaysConsumed / _giftIntervalDays - claimed <= 0)
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);

        // The claim is written before the gift is handed over: a row that is there for a gift
        // nobody received costs the player one gift, while a gift given with no row would let
        // them take every offer on the shelf.
        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            dbCtx.PlayerClubGifts.Add(
                new PlayerClubGiftEntity
                {
                    PlayerEntityId = playerId.Value,
                    CatalogOfferEntityId = offer.Id,
                }
            );

            await dbCtx.SaveChangesAsync(ct);
        }

        await _grainFactory
            .GetInventoryGrain(playerId)
            .GrantCatalogOfferAsync(offer, string.Empty, 1, ct);

        _logger.LogInformation(
            "Player {PlayerId} claimed club gift {OfferId} ({ProductCode})",
            playerId,
            offer.Id,
            offer.LocalizationId
        );

        return offer;
    }

    private async Task<int> CountClaimedGiftsAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        return await dbCtx
            .PlayerClubGifts.AsNoTracking()
            .CountAsync(x => x.PlayerEntityId == this.GetPlayerId().Value, ct);
    }
}
