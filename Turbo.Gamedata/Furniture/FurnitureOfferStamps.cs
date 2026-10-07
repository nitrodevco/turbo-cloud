using System.Collections.Generic;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture.Enums;

namespace Turbo.Gamedata.Furniture;

/// <summary>
/// Each furniture's offers in the two catalogs, for its furnidata item: <c>offerid</c> from the
/// normal catalog, <c>bcofferid</c> from the Builders Club one. The client's catalog search goes
/// furniture → offer id → the page that lists the offer, so an id is only given for an offer a
/// player can reach and buy:
/// <list type="bullet">
/// <item>the offer is visible, and listed on a page that catalog shows;</item>
/// <item>that page and every page above it are visible - an invisible page and what is under it
/// are not in the catalog's navigation, so neither are their offers;</item>
/// <item>it sells that one furniture: an offer of several (a bundle) is not the furniture's offer.</item>
/// </list>
/// A furniture several offers sell gets the one of a single item before a pack of several, then
/// the oldest. A page both catalogs show sells the same offer in each, so both ids are that offer.
/// The catalog snapshots are what players see: a catalog edited but not published yet stamps as
/// it was.
/// </summary>
internal static class FurnitureOfferStamps
{
    /// <summary>The offer id of each furniture definition one sells, by definition id.</summary>
    public static Dictionary<int, int> Of(CatalogSnapshot catalog)
    {
        var best = new Dictionary<int, (int OfferId, int Quantity)>();

        foreach (var page in catalog.PagesById.Values)
        {
            if (page.OfferIds.IsDefaultOrEmpty || !IsReachable(catalog, page))
                continue;

            foreach (var offerId in page.OfferIds)
            {
                if (
                    !catalog.OffersById.TryGetValue(offerId, out var offer)
                    || !offer.Visible
                    || offer.Products.Length != 1
                )
                    continue;

                var product = offer.Products[0];

                if (product.ProductType is not (ProductType.Floor or ProductType.Wall))
                    continue;

                var candidate = (offer.Id, product.Quantity);

                if (
                    !best.TryGetValue(product.FurniDefinitionId, out var current)
                    || IsBetter(candidate, current)
                )
                    best[product.FurniDefinitionId] = candidate;
            }
        }

        var stamps = new Dictionary<int, int>(best.Count);

        foreach (var (definitionId, offer) in best)
            stamps[definitionId] = offer.OfferId;

        return stamps;
    }

    private static bool IsBetter(
        (int OfferId, int Quantity) candidate,
        (int OfferId, int Quantity) current
    )
    {
        var candidateSingle = candidate.Quantity == 1;
        var currentSingle = current.Quantity == 1;

        if (candidateSingle != currentSingle)
            return candidateSingle;

        return candidate.OfferId < current.OfferId;
    }

    /// <summary>Whether the page and every page above it are visible.</summary>
    private static bool IsReachable(CatalogSnapshot catalog, CatalogPageSnapshot page)
    {
        // A tree loaded from rows could loop; no path is longer than there are pages.
        for (var steps = 0; steps <= catalog.PagesById.Count; steps++)
        {
            if (!page.Visible)
                return false;

            if (!catalog.PagesById.TryGetValue(page.ParentId, out var parent))
                return true;

            page = parent;
        }

        return false;
    }
}
