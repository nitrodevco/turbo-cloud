using System;
using System.Collections.Generic;
using System.Linq;

namespace Turbo.Catalog.Configuration;

/// <summary>
/// The hotel's targeted offers: the offer window a player is shown at login (Flash
/// <c>catalog/targetedoffers</c>, <c>offers.enabled</c>). None are listed by default, and a player
/// with no running offer is told <c>TargetedOfferNotFound</c>, which is what Habbo answered every
/// recorded login (official client capture, 2026-10-12, 4 logins).
/// </summary>
public sealed class TargetedOfferConfig
{
    public const string SECTION_NAME = "Turbo:TargetedOffers";

    /// <summary>When off, every player is told there is no offer and purchases are refused.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The offers, in the order they are offered: a player gets the first they can still buy.</summary>
    public List<TargetedOfferDefinition> Offers { get; set; } = [];

    /// <summary>What is wrong with the offers, or null when they can be used.</summary>
    public string? Problem()
    {
        if (Offers.Select(x => x.Id).Distinct().Count() != Offers.Count)
            return "offer ids must be unique";

        if (Offers.Any(x => x.Id <= 0 || x.CatalogOfferId <= 0))
            return "offer and catalogue offer ids must be positive";

        if (
            Offers.Any(x =>
                x.PriceInCredits < 0 || x.PriceInActivityPoints < 0 || x.PurchaseLimit < 1
            )
        )
            return "prices must not be negative and the purchase limit must be at least 1";

        return null;
    }
}

/// <summary>
/// One targeted offer: what the dialog shows (<c>TargetedOfferData</c>), its price, how many a
/// player may buy, when it runs, and the catalogue offer a purchase grants.
/// </summary>
public sealed class TargetedOfferDefinition
{
    public int Id { get; set; }

    /// <summary>The offer's code; the client logs it with "open credits page" clicks.</summary>
    public string Identifier { get; set; } = string.Empty;

    /// <summary>The catalogue offer (normal catalogue) a purchase grants, once per unit bought.</summary>
    public int CatalogOfferId { get; set; }

    /// <summary>The product code sent with the offer; the client shows nothing from it.</summary>
    public string ProductCode { get; set; } = string.Empty;

    public List<string> SubProductCodes { get; set; } = [];

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string ImageUrl { get; set; } = string.Empty;

    public string IconImageUrl { get; set; } = string.Empty;

    /// <summary><c>TargetedOfferData.type</c>; the client reads it but no view of 1318 uses it.</summary>
    public int Type { get; set; }

    public int PriceInCredits { get; set; }

    public int PriceInActivityPoints { get; set; }

    /// <summary>The activity point type of <see cref="PriceInActivityPoints"/> (0 duckets, 5 diamonds).</summary>
    public int ActivityPointType { get; set; }

    /// <summary>How many a player may buy in all; the dialog's quantity box shows above 1.</summary>
    public int PurchaseLimit { get; set; } = 1;

    /// <summary>From when it is offered (UTC); null: from the start.</summary>
    public DateTime? StartsAt { get; set; }

    /// <summary>Until when it is offered (UTC); null: no end, sent as 0 seconds left.</summary>
    public DateTime? EndsAt { get; set; }
}
