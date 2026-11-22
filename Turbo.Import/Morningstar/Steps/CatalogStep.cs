using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Catalog;
using Turbo.Import.Source;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture.Enums;

namespace Turbo.Import.Morningstar.Steps;

/// <summary>
/// With <c>--catalog</c>: the catalog's pages (<c>catalog_pages</c>), offers (<c>catalog_items</c>)
/// and limited editions, beside the pages the target has: Morningstar's top-level pages go under
/// Turbo's root. Ids are Turbo's own, after the target's.
/// <para>
/// A page keeps its layout as the client knows it, and its images and texts in the order
/// Morningstar's layout sent them (<see cref="LAYOUTS"/>): Morningstar stores seven named columns
/// and each layout picks its own. A page's rank is the security level of Morningstar's rank, as
/// Turbo gates a page by it. An offer of furniture, badges or effects is copied; pets, bots and
/// club offers are Turbo's own and are left out.
/// </para>
/// </summary>
internal sealed class CatalogStep : IImportStep
{
    // H header, T teaser, S special image; 1 text one, 2 text two, D details, X teaser text, _ empty.
    private static readonly Dictionary<
        string,
        (string Client, string Images, string Texts)
    > LAYOUTS = new(StringComparer.OrdinalIgnoreCase)
    {
        ["default_3x3"] = ("default_3x3", "HTS", "1DX"),
        ["badge_display"] = ("badge_display", "HTS", "1DX"),
        ["bots"] = ("bots", "HT", "1D2"),
        ["builders_club_addons"] = ("builders_club_addons", "HTS", "1DX"),
        ["builders_club_frontpage"] = ("builders_club_frontpage", "HTS", "1DX"),
        ["builders_club_loyalty"] = ("builders_club_loyalty", "HTS", "1DX"),
        ["club_buy"] = ("club_buy", "HT", ""),
        ["club_gift"] = ("club_gifts", "H", "1"),
        ["default_3x3_color_grouping"] = ("default_3x3_color_grouping", "HTS", "1DX"),
        ["frontpage"] = ("frontpage4", "HT", "12X"),
        ["frontpage_featured"] = ("frontpage_featured", "HTS", "1DX"),
        ["guild_forum"] = ("guild_forum", "HT", "1DX"),
        ["guilds"] = ("guild_frontpage", "HT", "1DX"),
        ["guild_furni"] = ("guild_custom_furni", "HT", "1DX"),
        ["info_duckets"] = ("info_duckets", "H", "1"),
        ["info_loyalty"] = ("info_loyalty", "H", "1"),
        ["info_pets"] = ("info_pets", "HT", "1_X"),
        ["info_rentables"] = ("info_rentables", "H", "1"),
        ["loyalty_vip_buy"] = ("loyalty_vip_buy", "HTS", "1DX"),
        ["mad_money"] = ("mad_money", "HT", "12"),
        ["marketplace"] = ("marketplace", "", ""),
        ["marketplace_own_items"] = ("marketplace_own_items", "", ""),
        ["monkey"] = ("monkey", "HTS", "1DX"),
        ["niko"] = ("monkey", "HTS", "1DX"),
        ["petcustomization"] = ("petcustomization", "HTS", "1DX"),
        ["pets"] = ("pets", "HT", "12DX"),
        ["pets2"] = ("pets2", "HT", "12DX"),
        ["pets3"] = ("pets3", "HT", "12DX"),
        ["productpage1"] = ("productpage1", "HT", "12DX"),
        ["recent_purchases"] = ("default_3x3", "HTS", "1DX"),
        ["recycler"] = ("recycler", "HT", "1"),
        ["recycler_info"] = ("recycler_info", "HTS", "1DX"),
        ["recycler_prizes"] = ("recycler_prizes", "HTS", "1DX"),
        ["roomads"] = ("roomads", "HT", "12"),
        ["single_bundle"] = ("single_bundle", "HT_", "1DX2"),
        ["sold_ltd_items"] = ("sold_ltd_items", "HTS", ""),
        ["soundmachine"] = ("soundmachine", "HT", "1D"),
        ["spaces_new"] = ("spaces_new", "HTS", "1DX"),
        ["trophies"] = ("trophies", "HTS", "1DX"),
        ["vip_buy"] = ("vip_buy", "HTS", "1DX"),
    };

    public string Name => "catalog";

    public bool Applies(ImportContext ctx) => ctx.Options.Catalog;

    public async Task RunAsync(ImportContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        var root = await db
            .CatalogPages.AsNoTracking()
            .Where(x => x.ParentEntityId == null)
            .OrderBy(x => x.Id)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false)
            ?? throw new ImportException(
                "The target catalog has no root page to put Morningstar's pages under. Open the catalog once in the admin panel, or import without --catalog."
            );

        var nextPage = await NextIdAsync(db.CatalogPages.Select(x => x.Id), ct)
            .ConfigureAwait(false);
        var nextOffer = await NextIdAsync(db.CatalogOffers.Select(x => x.Id), ct)
            .ConfigureAwait(false);
        var nextProduct = await NextIdAsync(db.CatalogProducts.Select(x => x.Id), ct)
            .ConfigureAwait(false);

        var levels = new Dictionary<int, int>();

        await foreach (
            var row in ctx.Source.ReadAsync("permissions", "id", ct).ConfigureAwait(false)
        )
            levels[row.Int("id")] = Math.Max(1, row.Int("level", row.Int("id")));

        // Parents before children, so a page's parent is always written first.
        var sourcePages = new List<SourceRow>();

        await foreach (
            var row in ctx.Source.ReadAsync("catalog_pages", "id", ct).ConfigureAwait(false)
        )
            sourcePages.Add(row.Detach());

        var pageIds = new Dictionary<int, int>();
        var pages = ctx.Batch<CatalogPageEntity>("catalog_pages");
        var pending = sourcePages;

        while (pending.Count > 0)
        {
            var next = new List<SourceRow>();

            foreach (var row in pending)
            {
                var parent = row.Int("parent_id", -1);
                int? parentId =
                    parent <= 0 ? root
                    : pageIds.TryGetValue(parent, out var p) ? p
                    : null;

                if (parentId is null)
                {
                    next.Add(row);

                    continue;
                }

                var id = nextPage++;

                pageIds[row.Int("id")] = id;
                await pages
                    .AddAsync(Page(ctx, row, id, parentId.Value, levels), ct)
                    .ConfigureAwait(false);
            }

            // What is left has no parent anywhere: a page under a deleted one.
            if (next.Count == pending.Count)
            {
                foreach (var row in next)
                    ctx.Report.Note(
                        "catalog page left out: its parent page is gone",
                        $"page {row.Int("id")}"
                    );

                break;
            }

            pending = next;
        }

        await pages.FlushAsync(ct).ConfigureAwait(false);

        await OffersAsync(ctx, pageIds, nextOffer, nextProduct, ct).ConfigureAwait(false);
    }

    private static CatalogPageEntity Page(
        ImportContext ctx,
        SourceRow row,
        int id,
        int parent,
        IReadOnlyDictionary<int, int> levels
    )
    {
        var layout = row.Text("page_layout", "default_3x3").Trim();

        if (!LAYOUTS.TryGetValue(layout, out var shape))
        {
            ctx.Report.Note(
                "catalog page given the default layout: Turbo has no layout of that name",
                $"page {row.Int("id")} '{layout}'"
            );
            shape = LAYOUTS["default_3x3"];
        }

        var rank = row.Int("min_rank", 1);

        return new CatalogPageEntity
        {
            Id = id,
            CreatedAt = ctx.Now,
            ParentEntityId = parent,
            Localization = Cut(row.Text("caption").Trim(), 50),
            Name = Cut(row.Text("caption_save").Trim(), 50) is { Length: > 0 } name ? name : null,
            Icon = Math.Max(0, row.Int("icon_image")),
            Layout = shape.Client,
            ImageData = [.. shape.Images.Select(x => Field(row, x))],
            TextData = [.. shape.Texts.Select(x => Field(row, x))],
            SortOrder = row.Int("order_num"),
            Display =
                row.Bool("visible", true) && row.Bool("enabled", true)
                    ? CatalogPageDisplay.Regular
                    : CatalogPageDisplay.Invisible,
            MinRank = rank <= 1 ? 1 : levels.GetValueOrDefault(rank, rank),
        };
    }

    private static string Field(SourceRow row, char field) =>
        field switch
        {
            'H' => row.Text("page_headline"),
            'T' => row.Text("page_teaser"),
            'S' => row.Text("page_special"),
            '1' => row.Text("page_text1"),
            '2' => row.Text("page_text2"),
            'D' => row.Text("page_text_details"),
            'X' => row.Text("page_text_teaser"),
            _ => string.Empty,
        };

    private static async Task OffersAsync(
        ImportContext ctx,
        Dictionary<int, int> pageIds,
        int nextOffer,
        int nextProduct,
        CancellationToken ct
    )
    {
        var offers = ctx.Batch<CatalogOfferEntity>("catalog_offers");
        var products = ctx.Batch<CatalogProductEntity>("catalog_products").After(offers);
        var series = ctx.Batch<LtdSeriesEntity>("ltd_series").After(products);

        await foreach (
            var row in ctx.Source.ReadAsync("catalog_items", "id", ct).ConfigureAwait(false)
        )
        {
            if (!pageIds.TryGetValue(row.Int("page_id"), out var page))
                continue;

            var contents = Contents(ctx, row);

            if (contents.Count == 0)
            {
                ctx.Report.Note(
                    "catalog offer left out: nothing in it Turbo sells this way (pets, bots, club)",
                    $"catalog item {row.Int("id")} '{row.Text("catalog_name")}'"
                );

                continue;
            }

            var points = Math.Max(0, row.Int("cost_points"));
            var offerId = nextOffer++;

            await offers
                .AddAsync(
                    new CatalogOfferEntity
                    {
                        Id = offerId,
                        CreatedAt = ctx.Now,
                        CatalogPageEntityId = page,
                        LocalizationId = row.Text("catalog_name").Trim(),
                        CostCredits = Math.Max(0, row.Int("cost_credits")),
                        CostCurrency = points,
                        CurrencyTypeId =
                            points > 0
                            && ctx.CurrencyTypes.TryGetValue(
                                row.Int("points_type"),
                                out var currency
                            )
                                ? currency
                                : null,
                        CanGift = contents.All(x => x.Base.Row.Bool("allow_gift", true)),
                        CanBundle = row.Bool("have_offer", true),
                        ClubLevel = row.Bool("club_only") ? 1 : 0,
                        Visible = true,
                        SortOrder = row.Int("order_number"),
                        Page = null!,
                    },
                    ct
                )
                .ConfigureAwait(false);

            var extra = row.Text("extradata").Trim();
            var limited = row.Int("limited_stack");

            foreach (var (item, quantity) in contents)
            {
                var productId = nextProduct++;
                var product = new CatalogProductEntity
                {
                    Id = productId,
                    CreatedAt = ctx.Now,
                    CatalogOfferEntityId = offerId,
                    ProductType = item.Type,
                    Quantity = quantity,
                    Offer = null!,
                };

                switch (item.Type)
                {
                    case ProductType.Floor or ProductType.Wall:
                        product.FurnitureDefinitionEntityId = ctx.Definitions[item.Id].Id;
                        product.ExtraParam = extra.Length == 0 ? null : extra;
                        break;
                    case ProductType.Badge:
                        product.ExtraParam = item.Name;
                        break;
                    case ProductType.Effect:
                        product.ExtraParam = item.Sprite.ToString(CultureInfo.InvariantCulture);
                        break;
                }

                await products.AddAsync(product, ct).ConfigureAwait(false);

                if (limited > 0 && item.Type is ProductType.Floor or ProductType.Wall)
                {
                    var remaining = Math.Max(0, limited - row.Int("limited_sells"));

                    await series
                        .AddAsync(
                            new LtdSeriesEntity
                            {
                                CreatedAt = ctx.Now,
                                CatalogProductEntityId = productId,
                                TotalQuantity = limited,
                                RemainingQuantity = remaining,
                                RaffleWindowSeconds = 30,
                                IsActive = remaining > 0,
                                IsRaffleFinished = remaining == 0,
                            },
                            ct
                        )
                        .ConfigureAwait(false);

                    limited = 0; // One series an offer.
                }
            }
        }

        await series.FlushAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// An offer's items: one <c>items_base</c> id (times <c>amount</c>), or a bundle
    /// <c>id:amount;id:amount</c>.
    /// </summary>
    private static List<(BaseItem Base, int Quantity)> Contents(ImportContext ctx, SourceRow row)
    {
        var contents = new List<(BaseItem, int)>();
        var raw = row.Text("item_ids").Trim();
        var bundle =
            raw.Contains(':', StringComparison.Ordinal)
            || raw.Contains(';', StringComparison.Ordinal);

        foreach (
            var part in raw.Split(
                [';', ','],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            )
        )
        {
            var fields = part.Split(':');

            if (
                !int.TryParse(fields[0], out var id) || !ctx.BaseItems.TryGetValue(id, out var item)
            )
                continue;

            if (
                item.Type is ProductType.Floor or ProductType.Wall
                && !ctx.Definitions.ContainsKey(id)
            )
                continue;

            if (
                item.Type
                is not (
                    ProductType.Floor
                    or ProductType.Wall
                    or ProductType.Badge
                    or ProductType.Effect
                )
            )
                continue;

            var quantity =
                bundle && fields.Length > 1 && int.TryParse(fields[1], out var amount) ? amount
                : bundle ? 1
                : row.Int("amount", 1);

            contents.Add((item, Math.Max(1, quantity)));
        }

        return contents;
    }

    private static async Task<int> NextIdAsync(IQueryable<int> ids, CancellationToken ct) =>
        (await ids.Select(x => (int?)x).MaxAsync(ct).ConfigureAwait(false) ?? 0) + 1;

    private static string Cut(string value, int length) =>
        value.Length > length ? value[..length] : value;
}
