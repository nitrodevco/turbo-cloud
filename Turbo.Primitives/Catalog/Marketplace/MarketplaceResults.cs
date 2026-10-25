using System.Collections.Immutable;

namespace Turbo.Primitives.Catalog.Marketplace;

/// <summary>A public search: at most the configured number of rows, and how many rows match in all.</summary>
public sealed record MarketplaceOfferPage(
    ImmutableArray<MarketplaceOfferSnapshot> Offers,
    int TotalItemsFound
);

/// <summary>A seller's offers of one category, and the credits their sold offers wait to pay out.</summary>
public sealed record MarketplaceOwnOffers(
    ImmutableArray<MarketplaceOfferSnapshot> Offers,
    int CreditsWaiting
);

/// <summary>Flash <c>MarketplaceModel.proceedOfferMaking</c>'s code, and the seller's tokens.</summary>
public sealed record MarketplaceCanMakeOffer(MarketplaceCanMakeOfferCode Code, int TokenCount);

/// <summary>
/// A purchase's outcome as Flash <c>MarketPlaceLogic.onBuyResult</c> reads it: on
/// <see cref="MarketplaceBuyResultCode.PriceChanged"/> the next cheapest offer of the kind and its price.
/// </summary>
public sealed record MarketplaceBuyResult(
    MarketplaceBuyResultCode Code,
    int OfferId,
    int NewPrice,
    int RequestedOfferId
);

public enum MarketplaceCanMakeOfferCode
{
    Allowed = 1,
    NoTradingPrivilege = 2,
    NoTradingPass = 3,
    NoTokens = 4,
}

/// <summary>The <c>inventory.marketplace.result.&lt;n&gt;</c> text a posting shows.</summary>
public enum MarketplaceMakeOfferCode
{
    Success = 1,
    TechnicalError = 2,
    Disabled = 3,

    /// <summary><c>inventory.marketplace.result.8</c>: the seller's account is safety locked.</summary>
    SafetyLocked = 8,
    TooManyItems = 9,
}

public enum MarketplaceBuyResultCode
{
    Bought = 1,
    SoldOut = 2,
    PriceChanged = 3,
    NotEnoughCredits = 4,
}
