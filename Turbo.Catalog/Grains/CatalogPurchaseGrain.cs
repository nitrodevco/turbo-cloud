using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Catalog.Exceptions;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Wallet;

namespace Turbo.Catalog.Grains;

public sealed partial class CatalogPurchaseGrain(
    IGrainFactory grainFactory,
    ICatalogService catalogService,
    ICatalogCurrencyTypeProvider currencyTypeProvider,
    ILogger<CatalogPurchaseGrain> logger
) : Grain, ICatalogPurchaseGrain
{
    private readonly ILogger<CatalogPurchaseGrain> _logger = logger;

    public override Task OnActivateAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    public async Task<CatalogOfferSnapshot> PurchaseOfferFromCatalogAsync(
        CatalogType catalogType,
        int offerId,
        string extraParam,
        int quantity,
        CancellationToken ct
    )
    {
        quantity = Math.Max(1, quantity);

        var snapshot = catalogService.GetCatalogSnapshot(catalogType);

        if (!snapshot.OffersById.TryGetValue(offerId, out var offer))
            throw new CatalogPurchaseException(CatalogPurchaseErrorType.OfferNotFound);

        var debitRequests = await BuildDebitRequestsAsync(offer, quantity, ct)
            .ConfigureAwait(false);
        var wallet = grainFactory.GetPlayerWalletGrain(this.GetPrimaryKeyLong());
        var debitResult = await wallet.TryDebitAsync(debitRequests, ct).ConfigureAwait(false);
        if (!debitResult.Succeeded)
            throw CreateInsufficientBalanceException(debitResult);

        var inventory = grainFactory.GetInventoryGrain(this.GetPrimaryKeyLong());

        try
        {
            await inventory
                .GrantCatalogOfferAsync(offer, extraParam, quantity, ct)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (!debitRequests.IsDefaultOrEmpty)
            {
                try
                {
                    await wallet.RefundAsync(debitRequests, ct).ConfigureAwait(false);
                }
                catch (Exception refundException)
                {
                    _logger.LogError(
                        refundException,
                        "Failed to refund wallet for player {PlayerId} after catalog grant failure. offerId={OfferId}, quantity={Quantity}",
                        this.GetPrimaryKeyLong(),
                        offer.Id,
                        quantity
                    );
                }
            }

            throw new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed, null, ex);
        }

        return offer;
    }

    private async Task<ImmutableArray<WalletDebitRequest>> BuildDebitRequestsAsync(
        CatalogOfferSnapshot offer,
        int quantity,
        CancellationToken ct
    )
    {
        var requests = ImmutableArray.CreateBuilder<WalletDebitRequest>(2);

        if (offer.CostCredits > 0)
        {
            var creditsCurrencyType = await currencyTypeProvider
                .GetCurrencyTypeByKeyAsync(WalletCurrencyKeyMapper.Credits, ct)
                .ConfigureAwait(false);
            if (creditsCurrencyType is not null && !creditsCurrencyType.Enabled)
                throw CreateOfferMisconfiguredException(
                    offer,
                    "Credits currency type is disabled.",
                    offer.CurrencyType
                );

            var requiredCredits = (long)offer.CostCredits * quantity;
            if (requiredCredits > int.MaxValue)
                throw CreateOfferMisconfiguredException(
                    offer,
                    "Credits cost overflowed int range.",
                    offer.CurrencyType
                );

            requests.Add(
                new WalletDebitRequest
                {
                    CurrencyType = WalletCurrencyKeyMapper.Credits,
                    Amount = (int)requiredCredits,
                    CurrencyKind = WalletCurrencyKind.Credits,
                    ActivityPointType = 0,
                    CurrencyTypeId = creditsCurrencyType?.Id,
                }
            );
        }

        if (offer.CostCurrency <= 0)
            return requests.ToImmutable();

        if (!offer.CurrencyType.HasValue)
            throw CreateOfferMisconfiguredException(
                offer,
                "Offer has cost_currency but no currency_type."
            );

        var currencyType = await currencyTypeProvider
            .GetCurrencyTypeAsync(offer.CurrencyType.Value, ct)
            .ConfigureAwait(false);
        if (currencyType is null || !currencyType.Enabled)
            throw CreateOfferMisconfiguredException(
                offer,
                "Currency type is missing or disabled.",
                offer.CurrencyType
            );

        if (!currencyType.IsActivityPoints)
            throw CreateOfferMisconfiguredException(
                offer,
                "Currency type is not marked as activity points.",
                offer.CurrencyType
            );

        if (!currencyType.ActivityPointType.HasValue)
            throw CreateOfferMisconfiguredException(
                offer,
                "Currency type has no activity point type.",
                offer.CurrencyType
            );
        var activityPointType = currencyType.ActivityPointType.Value;

        if (string.IsNullOrWhiteSpace(currencyType.CurrencyKey))
            throw CreateOfferMisconfiguredException(
                offer,
                "Currency type has an empty currency key.",
                offer.CurrencyType
            );

        var currencyKey = WalletCurrencyKeyMapper.ToCanonicalKey(
            currencyType.CurrencyKey,
            WalletCurrencyKind.ActivityPoints,
            activityPointType
        );

        var requiredCurrency = (long)offer.CostCurrency * quantity;
        if (requiredCurrency > int.MaxValue)
            throw CreateOfferMisconfiguredException(
                offer,
                "Secondary currency cost overflowed int range.",
                offer.CurrencyType
            );

        requests.Add(
            new WalletDebitRequest
            {
                CurrencyType = currencyKey,
                Amount = (int)requiredCurrency,
                CurrencyKind = WalletCurrencyKind.ActivityPoints,
                ActivityPointType = activityPointType,
                CurrencyTypeId = currencyType.Id,
            }
        );

        return requests.ToImmutable();
    }

    private CatalogPurchaseException CreateOfferMisconfiguredException(
        CatalogOfferSnapshot offer,
        string reason,
        int? currencyTypeId = null
    )
    {
        _logger.LogError(
            "Catalog offer misconfigured. offerId={OfferId}, pageId={PageId}, currencyTypeId={CurrencyTypeId}, costCredits={CostCredits}, costCurrency={CostCurrency}, reason={Reason}",
            offer.Id,
            offer.PageId,
            currencyTypeId,
            offer.CostCredits,
            offer.CostCurrency,
            reason
        );

        return new CatalogPurchaseException(CatalogPurchaseErrorType.OfferMisconfigured);
    }

    private static CatalogPurchaseException CreateInsufficientBalanceException(
        WalletDebitResult debitResult
    )
    {
        if (debitResult.Failure is null)
            return new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);

        if (debitResult.Failure.CurrencyKind == WalletCurrencyKind.Credits)
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

        if (debitResult.Failure.CurrencyKind == WalletCurrencyKind.ActivityPoints)
        {
            return new CatalogPurchaseException(
                CatalogPurchaseErrorType.NotEnoughActivityPoints,
                new CatalogBalanceFailure
                {
                    NotEnoughCredits = false,
                    NotEnoughActivityPoints = true,
                    ActivityPointType = debitResult.Failure.ActivityPointType,
                }
            );
        }

        return new CatalogPurchaseException(CatalogPurchaseErrorType.PurchaseFailed);
    }
}
