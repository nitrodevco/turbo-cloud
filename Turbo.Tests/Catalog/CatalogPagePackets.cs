using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Packets;
using Turbo.Tests.Support;

namespace Turbo.Tests.Catalog;

/// <summary>
/// A catalog page as a client gets it: asked for with the client's bytes, answered by the real
/// handler from a published catalog, and read back field by field in the order the client's
/// <c>CatalogPageMessageParser</c> reads them.
/// </summary>
internal static class CatalogPagePackets
{
    public sealed record Product(string Type, int SpriteId, string ExtraParam, int Quantity);

    public sealed record Offer(
        int Id,
        string LocalizationId,
        bool CanBundle,
        IReadOnlyList<Product> Products
    );

    public sealed record FrontPageItem(
        int Position,
        string Name,
        string Image,
        int Type,
        string Value,
        int ExpiresInSeconds
    );

    public sealed record Page(
        int Id,
        string Layout,
        IReadOnlyList<Offer> Offers,
        IReadOnlyList<FrontPageItem> FrontPageItems
    );

    public static async Task<Page> RequestAsync(
        CatalogSnapshot catalog,
        int pageId,
        TimeProvider? time = null
    )
    {
        var harness = new PacketHarness();

        harness.Fakes.Handlers["GetCatalogSnapshot"] = _ => catalog;
        harness.Resolver.Overrides[typeof(ICatalogService)] =
            harness.Fakes.Create<ICatalogService>();

        if (time is not null)
            harness.Resolver.Overrides[typeof(TimeProvider)] = time;

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("GetCatalogPageMessageEvent"),
            PacketHarness.Payload(w => w.Int(pageId).Int(-1).String("NORMAL"))
        );

        return Read(
            replies.Single(x => x.Header == PacketHarness.Outgoing("CatalogPageMessageComposer"))
        );
    }

    private static Page Read(ClientPacket packet)
    {
        var id = packet.PopInt();

        packet.PopString();

        var layout = packet.PopString();

        for (var lists = 0; lists < 2; lists++)
        {
            var lines = packet.PopInt();

            for (var i = 0; i < lines; i++)
                packet.PopString();
        }

        var offers = new List<Offer>();
        var offerCount = packet.PopInt();

        for (var i = 0; i < offerCount; i++)
            offers.Add(ReadOffer(packet));

        packet.PopInt();
        packet.PopBoolean();

        var items = new List<FrontPageItem>();
        var itemCount = packet.PopInt();

        for (var i = 0; i < itemCount; i++)
        {
            var position = packet.PopInt();
            var name = packet.PopString();
            var image = packet.PopString();
            var type = packet.PopInt();
            var value = type == 1 ? packet.PopInt().ToString() : packet.PopString();

            items.Add(new FrontPageItem(position, name, image, type, value, packet.PopInt()));
        }

        if (packet.Remaining != 0)
            throw new InvalidOperationException($"{packet.Remaining} bytes left unread");

        return new Page(id, layout, offers, items);
    }

    private static Offer ReadOffer(ClientPacket packet)
    {
        var id = packet.PopInt();
        var name = packet.PopString();

        packet.PopBoolean();
        packet.PopInt();
        packet.PopInt();
        packet.PopInt();
        packet.PopInt();
        packet.PopBoolean();

        var products = new List<Product>();
        var productCount = packet.PopInt();

        for (var i = 0; i < productCount; i++)
        {
            var type = packet.PopString();

            // A badge is its code and nothing else.
            if (type == "b")
            {
                products.Add(new Product(type, 0, packet.PopString(), 1));

                continue;
            }

            var sprite = packet.PopInt();
            var extra = packet.PopString();
            var quantity = packet.PopInt();

            if (packet.PopBoolean())
            {
                packet.PopInt();
                packet.PopInt();
            }

            products.Add(new Product(type, sprite, extra, quantity));
        }

        packet.PopInt();

        var canBundle = packet.PopBoolean();

        packet.PopBoolean();
        packet.PopString();

        return new Offer(id, name, canBundle, products);
    }
}
