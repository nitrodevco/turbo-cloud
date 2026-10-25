using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Contracts.Hotel;
using Turbo.Database.Context;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Players;

namespace Turbo.Main.Hotel;

/// <summary>
/// <see cref="IHotelCatalog"/> over the live catalog, the furni table (read only), the catalog
/// editor the panel uses and the expiring page widget.
/// </summary>
internal sealed class HostHotelCatalog(
    IDbContextFactory<TurboDbContext> database,
    ICatalogSnapshotProvider<NormalCatalog> catalog,
    ICatalogEditService edits,
    IExpiringPageService expiringPages
) : IHotelCatalog
{
    public IReadOnlyList<HotelOffer> Offers(int pageId)
    {
        var current = catalog.Current;

        if (!current.PageOfferIds.TryGetValue(pageId, out var offerIds))
            return [];

        return
        [
            .. offerIds
                .Select(id => current.OffersById.TryGetValue(id, out var offer) ? offer : null)
                .OfType<CatalogOfferSnapshot>()
                .Select(ToOffer),
        ];
    }

    public async Task<IReadOnlyDictionary<int, int>> CirculationAsync(
        IReadOnlyCollection<int> definitionIds,
        CancellationToken ct
    )
    {
        if (definitionIds.Count == 0)
            return new Dictionary<int, int>();

        var ids = definitionIds.Distinct().ToList();

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var counts = await db
            .Furnitures.AsNoTracking()
            .Where(x =>
                ids.Contains(x.FurnitureDefinitionEntityId)
                && !db.Players.Any(p => p.Id == x.PlayerEntityId && p.IsSystemAccount)
            )
            .GroupBy(x => x.FurnitureDefinitionEntityId)
            .Select(x => new { x.Key, Count = x.Count() })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return counts.ToDictionary(x => x.Key, x => x.Count);
    }

    public async Task<string?> MoveOfferAsync(
        PlayerId editor,
        int offerId,
        int pageId,
        int index,
        CancellationToken ct
    ) =>
        (await edits.MoveOfferAsync(editor, offerId, pageId, index, ct).ConfigureAwait(false))
            is { Saved: false } refused
            ? refused.Error ?? "refused"
            : null;

    public async Task SetPageExpiryAsync(
        int pageId,
        DateTime at,
        string image,
        CancellationToken ct
    ) => await expiringPages.SaveAsync(pageId, at, image, ct).ConfigureAwait(false);

    public async Task PublishAsync(PlayerId editor, CancellationToken ct) =>
        await edits.PublishAsync(editor, ct).ConfigureAwait(false);

    /// <summary>An offer of one furni, one of it, names its definition.</summary>
    private static HotelOffer ToOffer(CatalogOfferSnapshot offer)
    {
        var furni =
            offer.Products is [var only]
            && only.Quantity == 1
            && only.ProductType is ProductType.Floor or ProductType.Wall
                ? only.FurniDefinitionId
                : (int?)null;

        return new(
            offer.Id,
            offer.PageId,
            offer.LocalizationId,
            offer.CostCredits,
            offer.CostCurrency,
            offer.ActivityPointType,
            furni
        );
    }
}
