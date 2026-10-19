using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Turbo.Admin.Api.Contracts;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture.Enums;

namespace Turbo.Admin.Catalog;

/// <summary>
/// One catalogue out of a packet log of Habbo's client: the last index of it the client was sent
/// (<c>CatalogIndex</c>) and each page of it the client opened (<c>CatalogPage</c>), the last
/// one sent when a page was opened twice. Each is read as the client's
/// <c>CatalogIndexMessageEvent</c> and <c>CatalogPageMessageEvent</c> parsers read it; a packet
/// laid out otherwise (another client release's) is left out with a note, never read in part.
/// Pages and indexes of the other catalogue are left out with a note too, as are packets the log
/// itself could not give the bytes of.
/// </summary>
public static class CatalogPacketLogReader
{
    /// <summary>The names the index goes by in packet loggers.</summary>
    private static readonly string[] IndexNames =
    [
        "CatalogIndex",
        "CatalogPages",
        "CatalogPagesList",
    ];

    private const string PAGE_NAME = "CatalogPage";

    /// <summary>What a name may end in that is not part of the packet's name.</summary>
    private static readonly string[] NameSuffixes = ["MessageEvent", "Event", "Message"];

    /// <summary>How deep an index is read; Habbo's goes a few levels.</summary>
    private const int DEPTH_MAX = 32;

    /// <summary>How many of a note's lines or pages are named before the rest are counted.</summary>
    private const int LISTED_MAX = 5;

    /// <summary>The fewest bytes an index node, an offer and a product take.</summary>
    private const int NODE_BYTES_MIN = 1 + 4 + 4 + 2 + 2 + 4 + 4;
    private const int OFFER_BYTES_MIN = 4 + 2 + 1 + 4 * 4 + 1 + 4 + 4 + 1 + 1 + 2;
    private const int PRODUCT_BYTES_MIN = 2 + 2;
    private const int STRING_BYTES_MIN = 2;
    private const int FRONT_PAGE_ITEM_BYTES_MIN = 4 + 2 + 2 + 4 + 4;

    /// <summary>The front page item kinds, as the client's parser switches on them.</summary>
    private const int FRONT_PAGE_PAGE = 0;
    private const int FRONT_PAGE_OFFER = 1;
    private const int FRONT_PAGE_PRODUCT = 2;

    public static CatalogLogCapture Read(IReadOnlyList<LoggedPacket> log, CatalogType catalogType)
    {
        var wanted = catalogType.ToLegacyString();
        CatalogCaptureNode? root = null;
        var indexes = 0;
        var pages = new Dictionary<int, CatalogCapturePage>();
        var otherIndexes = 0;
        var otherPages = 0;
        var unreadable = new List<string>();
        var unlogged = new List<string>();

        foreach (var packet in log)
        {
            if (!packet.Incoming)
                continue;

            var name = NameOf(packet.Name);
            var isIndex = IndexNames.Contains(name, StringComparer.OrdinalIgnoreCase);

            if (!isIndex && !string.Equals(name, PAGE_NAME, StringComparison.OrdinalIgnoreCase))
                continue;

            if (packet.Payload is not { } payload)
            {
                unlogged.Add($"line {packet.Line} ({packet.Problem})");
                continue;
            }

            try
            {
                var reader = new PacketPayloadReader(payload);

                if (isIndex)
                {
                    var node = Node(reader, 0);
                    reader.ReadBoolean();
                    var type = reader.ReadString();
                    reader.End();

                    if (type != wanted)
                        otherIndexes++;
                    else
                    {
                        root = node;
                        indexes++;
                    }
                }
                else
                {
                    var (page, type) = Page(reader);

                    if (type != wanted)
                        otherPages++;
                    else
                        pages[page.PageId] = page;
                }
            }
            catch (InvalidDataException ex)
            {
                unreadable.Add($"line {packet.Line} ({ex.Message})");
            }
        }

        var notes = new List<string>();
        var other = catalogType == CatalogType.BuildersClub ? "normal" : "Builders Club";

        if (unlogged.Count > 0)
            notes.Add(
                $"{unlogged.Count} catalogue packets of the log could not be read from it, so what they held is left out: {Listed(unlogged)}."
            );

        if (unreadable.Count > 0)
            notes.Add(
                $"{unreadable.Count} catalogue packets are laid out differently from how this hotel reads them (another client release's?), so what they held is left out: {Listed(unreadable)}."
            );

        if (indexes > 1)
            notes.Add($"The log has the index {indexes} times over; the last one is taken in.");

        if (otherIndexes + otherPages > 0)
            notes.Add(
                $"The log also has the {other} catalogue's {(otherIndexes > 0 ? "index and " : string.Empty)}{otherPages} pages, which are left out here: give them as the {other} catalogue's log."
            );

        return new CatalogLogCapture(root, pages, notes);
    }

    /// <summary>A logger's name for a packet without what it ends in (<c>CatalogPageMessageEvent</c> is <c>CatalogPage</c>).</summary>
    private static string NameOf(string? name)
    {
        if (name is null)
            return string.Empty;

        foreach (var suffix in NameSuffixes)
        {
            if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
                return name[..^suffix.Length];
        }

        return name;
    }

    private static string Listed(List<string> items) =>
        items.Count <= LISTED_MAX
            ? string.Join("; ", items)
            : $"{string.Join("; ", items.Take(LISTED_MAX))} and {items.Count - LISTED_MAX} more";

    /// <summary>An index node and those under it: visible, icon, page id, name, title, offer ids, children.</summary>
    private static CatalogCaptureNode Node(PacketPayloadReader reader, int depth)
    {
        if (depth > DEPTH_MAX)
            throw new InvalidDataException($"the index goes deeper than {DEPTH_MAX} levels");

        var visible = reader.ReadBoolean();
        var icon = reader.ReadInt();
        var pageId = reader.ReadInt();
        var pageName = reader.ReadString();
        var localization = reader.ReadString();
        var offerIds = reader.ReadCount(sizeof(int));

        for (var i = 0; i < offerIds; i++)
            reader.ReadInt();

        var children = new CatalogCaptureNode[reader.ReadCount(NODE_BYTES_MIN)];

        for (var i = 0; i < children.Length; i++)
            children[i] = Node(reader, depth + 1);

        return new CatalogCaptureNode(visible, icon, pageId, pageName, localization, children);
    }

    /// <summary>
    /// A page: its id and catalogue, layout, images and texts, offers, the offer it opened on,
    /// whether season currency counts as credits, and the front page's items when it has them.
    /// </summary>
    private static (CatalogCapturePage Page, string CatalogType) Page(PacketPayloadReader reader)
    {
        var pageId = reader.ReadInt();
        var catalogType = reader.ReadString();
        var layout = reader.ReadString();
        var images = Strings(reader);
        var texts = Strings(reader);
        var offers = new CatalogCaptureOffer[reader.ReadCount(OFFER_BYTES_MIN)];

        for (var i = 0; i < offers.Length; i++)
            offers[i] = Offer(reader);

        reader.ReadInt();
        reader.ReadBoolean();

        if (reader.Remaining > 0)
        {
            var items = reader.ReadCount(FRONT_PAGE_ITEM_BYTES_MIN);

            for (var i = 0; i < items; i++)
                SkipFrontPageItem(reader);
        }

        reader.End();

        return (new CatalogCapturePage(pageId, layout, new(images, texts), offers), catalogType);
    }

    private static string[] Strings(PacketPayloadReader reader)
    {
        var strings = new string[reader.ReadCount(STRING_BYTES_MIN)];

        for (var i = 0; i < strings.Length; i++)
            strings[i] = reader.ReadString();

        return strings;
    }

    /// <summary>
    /// An offer: id, name key, rented, credits, activity points and their type, silver,
    /// giftable, products, club level, bundle purchase, a flag and a preview picture.
    /// </summary>
    private static CatalogCaptureOffer Offer(PacketPayloadReader reader)
    {
        var id = reader.ReadInt();
        var localizationId = reader.ReadString();
        var isRent = reader.ReadBoolean();
        var credits = reader.ReadInt();
        var activityPoints = reader.ReadInt();
        var activityPointType = reader.ReadInt();
        var silver = reader.ReadInt();
        var giftable = reader.ReadBoolean();
        var products = new CatalogCaptureProduct[reader.ReadCount(PRODUCT_BYTES_MIN)];

        for (var i = 0; i < products.Length; i++)
            products[i] = Product(reader);

        var clubLevel = reader.ReadInt();
        var bundle = reader.ReadBoolean();
        reader.ReadBoolean();
        reader.ReadString();

        return new CatalogCaptureOffer(
            id,
            localizationId,
            credits,
            activityPoints,
            activityPointType,
            giftable,
            bundle,
            clubLevel,
            products,
            isRent,
            silver
        );
    }

    /// <summary>
    /// A product: its type, then for a badge its code; for anything else its sprite (an
    /// effect's id), extra parameter, count, and a limited edition's series and what is left.
    /// </summary>
    private static CatalogCaptureProduct Product(PacketPayloadReader reader)
    {
        var type = reader.ReadString();

        if (type == ProductType.Badge.ToLegacyString())
            return new CatalogCaptureProduct(type, 0, reader.ReadString(), 1, null);

        var spriteId = reader.ReadInt();
        var extraParam = reader.ReadString();
        var quantity = reader.ReadInt();
        var limitedSize = 0;

        if (reader.ReadBoolean())
        {
            limitedSize = reader.ReadInt();
            reader.ReadInt();
        }

        return new CatalogCaptureProduct(type, spriteId, extraParam, quantity, null, limitedSize);
    }

    /// <summary>A front page item, which is not taken in: position, name, picture, kind, its value, and when it ends.</summary>
    private static void SkipFrontPageItem(PacketPayloadReader reader)
    {
        reader.ReadInt();
        reader.ReadString();
        reader.ReadString();

        switch (reader.ReadInt())
        {
            case FRONT_PAGE_PAGE:
            case FRONT_PAGE_PRODUCT:
                reader.ReadString();
                break;
            case FRONT_PAGE_OFFER:
                reader.ReadInt();
                break;
        }

        reader.ReadInt();
    }
}
