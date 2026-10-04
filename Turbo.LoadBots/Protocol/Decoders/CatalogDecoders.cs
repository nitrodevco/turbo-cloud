using System.Collections.Generic;
using Turbo.Primitives.Furniture.Enums;

namespace Turbo.LoadBots.Protocol.Decoders;

/// <summary>A page in the catalog tree and the offers it lists.</summary>
public sealed record CatalogNode(
    int PageId,
    string Name,
    bool Visible,
    IReadOnlyList<int> OfferIds,
    IReadOnlyList<CatalogNode> Children
);

/// <summary>One product in an offer: its type letter and sprite.</summary>
public sealed record CatalogProduct(ProductType Type, int SpriteId, int Quantity);

/// <summary>An offer on a catalog page.</summary>
public sealed record CatalogOffer(
    int OfferId,
    int CostCredits,
    int CostCurrency,
    int ClubLevel,
    IReadOnlyList<CatalogProduct> Products
);

/// <summary>An opened catalog page.</summary>
public sealed record CatalogPage(int PageId, string Layout, IReadOnlyList<CatalogOffer> Offers);

/// <summary>
/// Decoders for the catalog messages, mirroring
/// <c>Turbo.Revisions/Revision20260909/Serializers/Catalog</c>.
/// </summary>
public static class CatalogDecoders
{
    public static CatalogNode CatalogIndex(PacketReader reader) => Node(reader);

    private static CatalogNode Node(PacketReader reader)
    {
        var visible = reader.Bool();
        _ = reader.Int(); // icon
        var pageId = reader.Int();
        var name = reader.String();
        _ = reader.String(); // localization
        var offerIds = reader.Ints();
        var childCount = reader.Count();
        var children = new List<CatalogNode>(childCount);

        for (var i = 0; i < childCount; i++)
            children.Add(Node(reader));

        return new CatalogNode(pageId, name, visible, offerIds, children);
    }

    public static CatalogPage CatalogPage(PacketReader reader)
    {
        var pageId = reader.Int();
        _ = reader.String(); // catalog type
        var layout = reader.String();
        _ = reader.Strings(); // images
        _ = reader.Strings(); // texts

        var count = reader.Count();
        var offers = new List<CatalogOffer>(count);

        for (var i = 0; i < count; i++)
            offers.Add(Offer(reader, purchased: false));

        return new CatalogPage(pageId, layout, offers);
    }

    /// <summary>PurchaseOK: the offer as bought, without the silver cost or the trailing fields.</summary>
    public static CatalogOffer PurchaseOk(PacketReader reader) => Offer(reader, purchased: true);

    private static CatalogOffer Offer(PacketReader reader, bool purchased)
    {
        var offerId = reader.Int();
        _ = reader.String(); // localization id
        _ = reader.Bool(); // rentable
        var costCredits = reader.Int();
        var costCurrency = reader.Int();
        _ = reader.Int(); // currency type

        if (!purchased)
            _ = reader.Int(); // silver

        _ = reader.Bool(); // can gift

        var productCount = reader.Count();
        var products = new List<CatalogProduct>(productCount);

        for (var i = 0; i < productCount; i++)
            products.Add(Product(reader));

        var clubLevel = reader.Int();
        _ = reader.Bool(); // can bundle

        if (!purchased)
        {
            _ = reader.Bool();
            _ = reader.String(); // preview image
        }

        return new CatalogOffer(offerId, costCredits, costCurrency, clubLevel, products);
    }

    private static CatalogProduct Product(PacketReader reader)
    {
        var type = reader.String().FromLegacyString();

        if (type is ProductType.Badge)
        {
            _ = reader.String(); // badge code

            return new CatalogProduct(type, 0, 1);
        }

        var spriteId = reader.Int();
        _ = reader.String(); // extra param
        var quantity = reader.Int();

        if (reader.Bool())
        {
            _ = reader.Int(); // limited series size
            _ = reader.Int(); // remaining
        }

        return new CatalogProduct(type, spriteId, quantity);
    }
}
