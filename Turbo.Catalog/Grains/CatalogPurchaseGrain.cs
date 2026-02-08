using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Catalog.Exceptions;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Catalog.Grains;

public sealed partial class CatalogPurchaseGrain(
    IGrainFactory grainFactory,
    ICatalogService catalogService,
    ILogger<CatalogPurchaseGrain> logger
) : Grain, ICatalogPurchaseGrain
{
    private readonly IGrainFactory _grainFactory = grainFactory;
    private readonly ICatalogService _catalogService = catalogService;
    private readonly ILogger<CatalogPurchaseGrain> _logger = logger;

    public async Task<CatalogOfferSnapshot> PurchaseOfferFromCatalogAsync(
        CatalogType catalogType,
        int offerId,
        string extraParam,
        int quantity,
        CancellationToken ct
    )
    {
        quantity = Math.Max(1, quantity);

        var snapshot = _catalogService.GetCatalogSnapshot(catalogType);

        if (!snapshot.OffersById.TryGetValue(offerId, out var offer))
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.OfferNotFound);

        if (TryGetDebitRequests(offer, quantity, out var debitRequests))
        {
            var result = await _grainFactory
                .GetPlayerWalletGrain((int)this.GetPrimaryKeyLong())
                .TryDebitAsync(debitRequests, ct);

            if (!result.Succeeded)
                throw CreateInsufficientBalanceException(result);
        }

        await _grainFactory
            .GetInventoryGrain((int)this.GetPrimaryKeyLong())
            .GrantCatalogOfferAsync(offer, extraParam, quantity, ct)
            .ConfigureAwait(false);

        return offer;
    }

    private bool TryGetDebitRequests(
        CatalogOfferSnapshot offer,
        int quantity,
        out List<WalletDebitRequest> requests
    )
    {
        requests = [];

        if (offer.CostCredits > 0)
            requests.Add(
                new WalletDebitRequest
                {
                    CurrencyKind = new CurrencyKind { CurrencyType = CurrencyType.Credits },
                    Amount = offer.CostCredits * quantity,
                }
            );

        if (offer.CostSilver > 0)
            requests.Add(
                new WalletDebitRequest
                {
                    CurrencyKind = new CurrencyKind { CurrencyType = CurrencyType.Silver },
                    Amount = offer.CostSilver * quantity,
                }
            );

        if (offer.CostCurrency > 0)
            requests.Add(
                new WalletDebitRequest
                {
                    CurrencyKind = new CurrencyKind
                    {
                        CurrencyType = CurrencyType.ActivityPoints,
                        ActivityPointType = offer.CurrencyTypeId,
                    },
                    Amount = offer.CostCurrency * quantity,
                }
            );

        return true;
    }

    private static CatalogPurchaseException CreateInsufficientBalanceException(
        WalletDebitResult debitResult
    )
    {
        if (debitResult.Failure is null)
            return new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);

        if (debitResult.Failure.CurrencyKind.CurrencyType == CurrencyType.Credits)
        {
            return new CatalogPurchaseException(
                CatalogPurchaseErrorType.NotEnoughCredits,
                new CatalogBalanceFailure
                {
                    NotEnoughCredits = true,
                    NotEnoughActivityPoints = false,
                    ActivityPointType = 0,
                }
            );
        }

        if (debitResult.Failure.CurrencyKind.CurrencyType == CurrencyType.ActivityPoints)
        {
            return new CatalogPurchaseException(
                CatalogPurchaseErrorType.NotEnoughActivityPoints,
                new CatalogBalanceFailure
                {
                    NotEnoughCredits = false,
                    NotEnoughActivityPoints = true,
                    ActivityPointType = debitResult.Failure.CurrencyKind.ActivityPointType ?? -1,
                }
            );
        }

        return new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);
    }
}
