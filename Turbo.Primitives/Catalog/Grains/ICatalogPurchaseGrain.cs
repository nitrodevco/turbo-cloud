using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Catalog.Grains;

public partial interface ICatalogPurchaseGrain : IGrainWithIntegerKey
{
    public Task<CatalogOfferSnapshot> PurchaseOfferFromCatalogAsync(
        CatalogType catalogType,
        int offerId,
        string extraParam,
        int quantity,
        CancellationToken ct
    );

    /// <summary>
    /// Charges the room-ad offer and starts (or extends) a promoted event in the player's room.
    /// Throws <c>CatalogPurchaseException</c> when the offer, room or balance is not valid.
    /// </summary>
    public Task<CatalogOfferSnapshot> PurchaseRoomAdAsync(
        int offerId,
        RoomId roomId,
        int categoryId,
        string name,
        string description,
        bool extended,
        TimeSpan duration,
        CancellationToken ct
    );
}
