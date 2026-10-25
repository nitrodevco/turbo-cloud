using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Contracts.Hotel;

/// <summary>
/// An offer of the live catalog: its page, its name (the offer's localization id), its price in
/// credits and in the activity point type it names (duckets, diamonds), and
/// the one furni it gives (null for a bundle, a badge, club or anything that is not one furni).
/// </summary>
public sealed record HotelOffer(
    int Id,
    int PageId,
    string Name,
    int CostCredits,
    int CostCurrency,
    int? ActivityPointType,
    int? FurnitureDefinitionId
);

/// <summary>
/// The catalog: reading the live one, how many of a furni players hold, and the edits staff make in
/// the panel's catalog editor, which this makes as the editor it is given, with the same checks
/// and history. An edit is seen by players after <see cref="PublishAsync"/>.
/// </summary>
public interface IHotelCatalog
{
    /// <summary>The live offers of the page, in their order; empty for a page there isn't.</summary>
    [HotelCall(HotelCallContext.Anywhere)]
    IReadOnlyList<HotelOffer> Offers(int pageId);

    /// <summary>
    /// How many of each furni definition players hold (in inventories and rooms), system accounts
    /// left out; a definition no one holds is not listed.
    /// </summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<IReadOnlyDictionary<int, int>> CirculationAsync(
        IReadOnlyCollection<int> definitionIds,
        CancellationToken ct
    );

    /// <summary>Puts an offer on a page at an index among its offers; null when saved, else why not.</summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task<string?> MoveOfferAsync(
        PlayerId editor,
        int offerId,
        int pageId,
        int index,
        CancellationToken ct
    );

    /// <summary>The reception's expiring page widget counts down to <paramref name="at"/> (UTC) for the page.</summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task SetPageExpiryAsync(int pageId, DateTime at, string image, CancellationToken ct);

    /// <summary>Puts the saved catalog live and tells every client online to refresh.</summary>
    [HotelCall(HotelCallContext.OffGrain)]
    Task PublishAsync(PlayerId editor, CancellationToken ct);
}
