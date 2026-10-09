using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Admin.Catalog;

/// <summary>
/// The catalog generator: a whole catalog worked out from the hotel's furniture, pets, effects and
/// songs, tab by tab - a front page; the furni, by furni line, lines of one family (every
/// Christmas, every NFT drop) under one folder; wired by kind; pets and their accessories; the
/// club shop and its gifts; effects, songs and bots; rares and limited items; the Builders Club;
/// groups - each page with the layout and icon that suit it, and furni lines too long for one page
/// spread over numbered pages. Planned first, to look over; made as one step to undo.
/// </summary>
public sealed partial class AdminCatalogBuilder
{
    public const string MODE_REPLACE = "replace";
    public const string MODE_ALONGSIDE = "alongside";

    public const int PER_PAGE_MIN = 20;
    public const int PER_PAGE_MAX = 500;

    public const string SECTION_FRONT = "front";
    public const string SECTION_FURNI = "furni";
    public const string SECTION_WIRED = "wired";
    public const string SECTION_PETS = "pets";
    public const string SECTION_CLUB = "club";
    public const string SECTION_EXTRAS = "extras";
    public const string SECTION_RARES = "rares";
    public const string SECTION_BUILDERS = "builders";
    public const string SECTION_GROUPS = "groups";

    /// <summary>The tabs a generated catalog can have, in the order it has them.</summary>
    public static readonly string[] GenerateSections =
    [
        SECTION_FRONT,
        SECTION_CLUB,
        SECTION_FURNI,
        SECTION_WIRED,
        SECTION_PETS,
        SECTION_EXTRAS,
        SECTION_RARES,
        SECTION_GROUPS,
        SECTION_BUILDERS,
    ];

    private const string ARCHIVE_TITLE = "Old catalog";
    private const string DEFAULT_LAYOUT = "default_3x3";
    private const string WIRED_PREFIX = "wf_";

    /// <summary>The client's icon_&lt;n&gt; for each kind of page, picked by sight from its icon set.</summary>
    private static class Icons
    {
        public const int FRONT = 64;
        public const int FURNI = 2;
        public const int SPACES = 41;
        public const int POSTERS = 63;
        public const int TROPHIES = 60;
        public const int BADGE_DISPLAYS = 106;
        public const int WIRED = 80;
        public const int WIRED_EFFECTS = 324;
        public const int PETS = 8;
        public const int PET_ACCESSORIES = 315;
        public const int PET_CARE = 24;
        public const int CLUB = 9;
        public const int CLUB_GIFTS = 217;
        public const int EXTRAS = 51;
        public const int EFFECTS = 51;
        public const int SONGS = 308;
        public const int BOTS = 286;
        public const int RARES = 92;
        public const int LIMITED = 145;
        public const int SOLD_OUT = 198;
        public const int BUILDERS = 323;
        public const int GROUPS = 201;
        public const int ARCHIVE = 26;
        public const int CHRISTMAS = 316;
        public const int HALLOWEEN = 34;
        public const int EASTER = 25;
        public const int SUMMER = 45;
        public const int VALENTINE = 144;
        public const int NFT = 206;
    }

    /// <summary>A furnidata category's page title and icon, for pages of furni in no line and a line's icon.</summary>
    private static readonly Dictionary<string, (string Title, int Icon)> Categories = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["chair"] = ("Chairs", 14),
        ["table"] = ("Tables", 35),
        ["bed"] = ("Beds", 19),
        ["lighting"] = ("Lighting", 115),
        ["divider"] = ("Dividers", 113),
        ["gate"] = ("Gates", 15),
        ["vending_machine"] = ("Vending machines", 11),
        ["rug"] = ("Rugs", 52),
        ["games"] = ("Games", 56),
        ["music"] = ("Music", 4),
        ["floor"] = ("Floors", 41),
        ["shelf"] = ("Shelves", 27),
        ["credit"] = ("Credit furni", 6),
        ["food"] = ("Food", 43),
        ["teleport"] = ("Teleports", 47),
        ["window"] = ("Windows", 47),
        ["sound_fx"] = ("Sound effects", 4),
        ["present"] = ("Presents", 100),
        ["roller"] = ("Rollers", 116),
        ["tent"] = ("Tents", 30),
        ["extras"] = ("Extras", 84),
        ["fortuna"] = ("Fortune", 13),
        ["leaderboards"] = ("Leaderboards", 60),
        ["dimmer"] = ("Dimmers", 74),
        ["wall_decoration"] = ("Wall decoration", 63),
        ["trophy"] = ("Trophies", 60),
        ["pets"] = ("Pet care", 24),
    };

    /// <summary>Words Habbo's furni line names shorten, as a page title spells them.</summary>
    private static readonly Dictionary<string, string> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["xmas"] = "Christmas",
        ["hween"] = "Habboween",
        ["habboween"] = "Habboween",
        ["val"] = "Valentine",
        ["nft"] = "NFT",
        ["hc"] = "HC",
        ["bc"] = "BC",
        ["ltd"] = "LTD",
        ["vip"] = "VIP",
        ["ad"] = "Ads",
        ["hhistory"] = "Habbo History",
        ["buildersclub"] = "Builders Club",
        ["cny"] = "Lunar New Year",
        ["newyear"] = "New Year",
        ["usa"] = "USA",
        ["uk"] = "UK",
    };

    /// <summary>A line family's folder icon, by the stem its lines share.</summary>
    private static readonly Dictionary<string, int> FamilyIcons = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        ["xmas"] = Icons.CHRISTMAS,
        ["christmas"] = Icons.CHRISTMAS,
        ["hween"] = Icons.HALLOWEEN,
        ["habboween"] = Icons.HALLOWEEN,
        ["easter"] = Icons.EASTER,
        ["summer"] = Icons.SUMMER,
        ["val"] = Icons.VALENTINE,
        ["valentine"] = Icons.VALENTINE,
        ["nft"] = Icons.NFT,
    };

    /// <summary>Wired by the start of its class name, as the wired pages divide it.</summary>
    private static readonly (string Prefix, string Key, string Title, int Icon)[] WiredKinds =
    [
        ("wf_trg_", "triggers", "Triggers", Icons.WIRED),
        ("wf_act_", "effects", "Effects", Icons.WIRED_EFFECTS),
        ("wf_cnd_", "conditions", "Conditions", Icons.WIRED),
        ("wf_xtra_", "addons", "Add-ons", Icons.WIRED),
        ("wf_slc_", "selectors", "Selectors", Icons.WIRED),
        ("wf_var_", "variables", "Variables", Icons.WIRED),
    ];

    /// <summary>The furni line of the club gifts members claim: never sold.</summary>
    private const string CLUB_GIFTS_LINE = "habbo_club_gifts";

    /// <summary>Furni lines that are the hotel's own tests, never sold.</summary>
    private static readonly HashSet<string> UnsoldLines = new(StringComparer.OrdinalIgnoreCase)
    {
        CLUB_GIFTS_LINE,
        "testing",
        "test",
    };

    /// <summary>The generated catalog as it would be, planned from the hotel as it is; nothing is changed.</summary>
    public async Task<CatalogBuildOutcome<CatalogGeneratePlan>> PlanCatalogAsync(
        CatalogGenerateRequest request,
        CancellationToken ct
    )
    {
        if (CheckGenerate(request) is { } refused)
            return CatalogBuildOutcome<CatalogGeneratePlan>.Refused(refused);

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var generated = await GenerateAsync(db, request, ct).ConfigureAwait(false);

        return CatalogBuildOutcome<CatalogGeneratePlan>.Done(
            new CatalogGeneratePlan(
                [.. generated.Tabs.Select(Describe)],
                [.. generated.Warnings],
                generated.Tabs.Sum(x => x.Count),
                generated.Tabs.Sum(x => x.NewOffers),
                generated.Tabs.Sum(x => x.MovedOffers),
                generated.Archive.Count
            )
        );

        static CatalogGeneratedPage Describe(GenPage page) =>
            new(
                page.Key,
                page.Section,
                page.Title,
                page.Name,
                page.Icon,
                page.Layout,
                page.Display.ToName(),
                page.Offers.Count,
                page.Moves.Count,
                [.. page.Children.Select(Describe)]
            );
    }

    /// <summary>
    /// Makes the generated catalog as planned, through <see cref="ICatalogEditService.BuildTreeAsync"/>:
    /// checked as a whole and saved as a whole, one step to undo.
    /// </summary>
    public async Task<CatalogBuildOutcome<CatalogGenerateResponse>> GenerateCatalogAsync(
        PlayerId editorId,
        CatalogGenerateRequest request,
        CancellationToken ct
    )
    {
        if (CheckGenerate(request) is { } refused)
            return CatalogBuildOutcome<CatalogGenerateResponse>.Refused(refused);

        Generated generated;
        int rootId;
        List<(int Id, string Name)> named;
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);

        await using (db.ConfigureAwait(false))
        {
            generated = await GenerateAsync(db, request, ct).ConfigureAwait(false);
            rootId = generated.RootId;

            // The names the client opens pages by go to the new pages; old pages keep none.
            var names = generated
                .Tabs.SelectMany(x => x.All)
                .Select(x => x.Name)
                .OfType<string>()
                .ToList();

            named = generated.Replace
                ?
                [
                    .. (
                        await db
                            .CatalogPages.AsNoTracking()
                            .Where(x => x.Name != null && names.Contains(x.Name))
                            .Select(x => new { x.Id, x.Name })
                            .ToListAsync(ct)
                            .ConfigureAwait(false)
                    ).Select(x => (x.Id, x.Name!)),
                ]
                : [];
        }

        if (rootId == 0)
            return CatalogBuildOutcome<CatalogGenerateResponse>.Refused(
                "This catalog has no root page."
            );

        var draft = ToDraft(generated, rootId);

        return await editor
            .GroupAsync(
                editorId,
                generated.Replace
                    ? "generated a fresh catalog"
                    : "generated a catalog beside this one",
                async () =>
                {
                    var built = await editor
                        .BuildTreeAsync(editorId, "built the generated catalog", draft, ct)
                        .ConfigureAwait(false);

                    if (!built.Saved)
                        return CatalogBuildOutcome<CatalogGenerateResponse>.Refused(built.Error!);

                    foreach (var (id, _) in named)
                        await ClearNameAsync(editorId, id, ct).ConfigureAwait(false);

                    return CatalogBuildOutcome<CatalogGenerateResponse>.Done(
                        new CatalogGenerateResponse(
                            built.PageIds.Count,
                            built.OffersCreated,
                            built.OffersMoved,
                            built.PagesMoved,
                            editor.UnpublishedChanges
                        )
                    );
                }
            )
            .ConfigureAwait(false);
    }

    /// <summary>An old page's name taken off, so the page of that name the client opens is the new one.</summary>
    private async Task ClearNameAsync(PlayerId editorId, int pageId, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var page = await db
            .CatalogPages.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == pageId, ct)
            .ConfigureAwait(false);

        if (page is null)
            return;

        await editor
            .UpdatePageAsync(
                editorId,
                pageId,
                new CatalogPageDraft(
                    page.Localization,
                    null,
                    page.Icon,
                    page.Layout,
                    page.ImageData ?? [],
                    page.TextData ?? [],
                    page.Display
                ),
                ct
            )
            .ConfigureAwait(false);
    }

    private static string? CheckGenerate(CatalogGenerateRequest request)
    {
        if (request.Mode is not (MODE_REPLACE or MODE_ALONGSIDE))
            return "Generate a catalog to replace this one, or beside it.";

        if (request.MaxPerPage is < PER_PAGE_MIN or > PER_PAGE_MAX)
            return $"A page sells {PER_PAGE_MIN} to {PER_PAGE_MAX} offers at most.";

        if (request.CostCredits < 0 || request.CostCurrency < 0)
            return "A price is 0 or more.";

        if (request.CostCurrency > 0 && request.CurrencyTypeId is null)
            return "Say which currency the second price is in.";

        return request.Sections is { } sections && !sections.Any(GenerateSections.Contains)
            ? "Pick at least one tab to generate."
            : null;
    }

    /// <summary>The generated tree as one draft: the tabs under the root, then the old tabs moved under the archive.</summary>
    private static CatalogTreeDraft ToDraft(Generated generated, int rootId)
    {
        var pages = new List<CatalogNewPage>();
        var offers = new List<CatalogNewOffer>();
        var moves = new List<CatalogOfferPlacement>();
        var price = generated.Request;

        void Add(GenPage page, CatalogPageRef parent)
        {
            var index = pages.Count;

            pages.Add(
                new CatalogNewPage(
                    parent,
                    new CatalogPageDraft(
                        page.Title,
                        page.Name,
                        page.Icon,
                        page.Layout,
                        page.Images,
                        page.Texts,
                        page.Display
                    )
                )
            );

            var self = CatalogPageRef.Made(index);

            foreach (var offer in page.Offers)
            {
                offers.Add(
                    new CatalogNewOffer(
                        self,
                        new CatalogOfferDraft(
                            0,
                            offer.LocalizationId,
                            price.CostCredits,
                            price.CostCurrency,
                            price.CostCurrency > 0 ? price.CurrencyTypeId : null,
                            CanGift: true,
                            CanBundle: true,
                            ClubLevel: 0,
                            Visible: true,
                            Product: null,
                            ClubGiftDaysRequired: null,
                            Products: offer.Gives
                        )
                    )
                );
            }

            moves.AddRange(page.Moves.Select(x => new CatalogOfferPlacement(x, self)));

            foreach (var child in page.Children)
                Add(child, self);
        }

        foreach (var tab in generated.Tabs)
            Add(tab, CatalogPageRef.Saved(rootId));

        var pageMoves = new List<CatalogPagePlacement>();

        if (generated.Archive.Count > 0)
        {
            var archive = pages.Count;

            pages.Add(
                new CatalogNewPage(
                    CatalogPageRef.Saved(rootId),
                    new CatalogPageDraft(
                        ARCHIVE_TITLE,
                        null,
                        Icons.ARCHIVE,
                        DEFAULT_LAYOUT,
                        [],
                        [],
                        CatalogPageDisplay.Invisible
                    )
                )
            );
            pageMoves.AddRange(
                generated.Archive.Select(x => new CatalogPagePlacement(
                    x,
                    CatalogPageRef.Made(archive)
                ))
            );
        }

        return new CatalogTreeDraft(pages, offers, moves, pageMoves);
    }

    /// <summary>The generated catalog: its tabs, the tabs it puts away, and what to look at first.</summary>
    private sealed record Generated(
        CatalogGenerateRequest Request,
        bool Replace,
        int RootId,
        List<GenPage> Tabs,
        List<int> Archive,
        List<string> Warnings
    );

    /// <summary>A furniture definition as the generator sorts it.</summary>
    private sealed record GenFurni(
        int Id,
        string Name,
        ProductType Type,
        string? Line,
        string? Category,
        string Logic,
        FurnitureCategory Kind
    );

    private sealed record GenOffer(string LocalizationId, IReadOnlyList<CatalogProductDraft> Gives);

    /// <summary>A page the generator plans, with what it will sell and the pages under it.</summary>
    private sealed class GenPage(string key, string section, string title, int icon, string layout)
    {
        public string Key { get; } = key;

        public string Section { get; } = section;

        public string Title { get; set; } = title;

        public string? Name { get; set; }

        public int Icon { get; set; } = icon;

        public string Layout { get; set; } = layout;

        public CatalogPageDisplay Display { get; set; } = CatalogPageDisplay.Regular;

        public List<string> Images { get; init; } = [];

        public List<string> Texts { get; init; } = [];

        public List<GenOffer> Offers { get; } = [];

        public List<int> Moves { get; } = [];

        public List<GenPage> Children { get; } = [];

        public IEnumerable<GenPage> All => Children.SelectMany(x => x.All).Prepend(this);

        public int Count => All.Count();

        public int NewOffers => All.Sum(x => x.Offers.Count);

        public int MovedOffers => All.Sum(x => x.Moves.Count);

        public bool IsEmpty => NewOffers == 0 && MovedOffers == 0;

        public GenPage Child(string key, string title, int icon, string layout)
        {
            var child = new GenPage($"{Key}/{key}", Section, title, icon, layout);

            Children.Add(child);

            return child;
        }
    }

    /// <summary>The offers there now that a generated page can take as they are, by what they give.</summary>
    private sealed class Reusable(
        Dictionary<(ProductType, int, string), Queue<int>> offers,
        bool enabled
    )
    {
        /// <summary>An offer that gives exactly this, taken so no other page takes it; null for none.</summary>
        public int? Take(CatalogProductDraft product)
        {
            if (!enabled)
                return null;

            return
                offers.TryGetValue(
                    Key(product.Type, product.DefinitionId, product.ExtraParam),
                    out var queue
                ) && queue.TryDequeue(out var id)
                ? id
                : null;
        }

        public static (ProductType, int, string) Key(
            ProductType type,
            int? definitionId,
            string? param
        ) => (type, definitionId ?? 0, param?.Trim() ?? string.Empty);
    }

    private async Task<Generated> GenerateAsync(
        TurboDbContext db,
        CatalogGenerateRequest request,
        CancellationToken ct
    )
    {
        var replace = request.Mode == MODE_REPLACE;
        var sections = (request.Sections ?? GenerateSections).ToHashSet(StringComparer.Ordinal);
        var warnings = new List<string>();
        var tabs = new List<GenPage>();
        var perPage = request.MaxPerPage;

        var pages = await db
            .CatalogPages.AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.ParentEntityId,
                x.Name,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var rootId = pages.Where(x => x.ParentEntityId is null).MinBy(x => x.Id)?.Id ?? 0;
        var takenNames = pages
            .Select(x => x.Name)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);

        // In replace mode the old pages lose their names; beside them, a name already taken stays with its page.
        string? Named(string name) => replace || !takenNames.Contains(name) ? name : null;

        var furni = await db
            .FurnitureDefinitions.AsNoTracking()
            .Where(x => x.ProductType == ProductType.Floor || x.ProductType == ProductType.Wall)
            .Select(x => new GenFurni(
                x.Id,
                x.Name,
                x.ProductType,
                x.FurniLine,
                x.ClientCategory,
                x.Logic,
                x.FurniCategory
            ))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        furni =
        [
            .. furni
                .Where(x =>
                    !ProductStuffData.IsNamedByProduct(x.Kind)
                    && !PetProductCodes.TryGetTypeId(x.Name, out _)
                    && !UnsoldLines.Contains(x.Line ?? string.Empty)
                )
                .OrderBy(x => x.Name, StringComparer.Ordinal),
        ];

        var offers = await db
            .CatalogOffers.AsNoTracking()
            .Select(x => new
            {
                x.Id,
                Gift = x.ClubGiftDaysRequired != null,
                Products = x.Products!.Select(p => new
                    {
                        p.Id,
                        p.ProductType,
                        p.FurnitureDefinitionEntityId,
                        p.ExtraParam,
                        p.SubscriptionType,
                    })
                    .ToList(),
            })
            .OrderBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var series = await db
            .LtdSeries.AsNoTracking()
            .Select(x => new
            {
                x.CatalogProductEntityId,
                x.RemainingQuantity,
                x.IsActive,
                x.StartsAt,
                x.CreatedAt,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        // A product's series as the catalog picks it: the active one, else the newest.
        var seriesOf = series
            .GroupBy(x => x.CatalogProductEntityId)
            .ToDictionary(
                g => g.Key,
                g =>
                    g.OrderByDescending(x => x.IsActive)
                        .ThenByDescending(x => x.StartsAt ?? x.CreatedAt)
                        .First()
            );
        var limited = offers.Where(x => x.Products.Any(p => seriesOf.ContainsKey(p.Id))).ToList();
        var reusable = new Reusable(
            offers
                .Where(x =>
                    !x.Gift
                    && x.Products.Count == 1
                    && !seriesOf.ContainsKey(x.Products[0].Id)
                    && x.Products[0].SubscriptionType is null
                )
                .GroupBy(x =>
                    Reusable.Key(
                        x.Products[0].ProductType,
                        x.Products[0].FurnitureDefinitionEntityId,
                        x.Products[0].ExtraParam
                    )
                )
                .ToDictionary(g => g.Key, g => new Queue<int>(g.Select(x => x.Id))),
            replace && request.ReuseOffers
        );
        var placed = new HashSet<int>();

        void Place(GenPage page, string localizationId, IReadOnlyList<CatalogProductDraft> gives)
        {
            foreach (var product in gives.Where(x => x.DefinitionId is not null))
                placed.Add(product.DefinitionId!.Value);

            if (gives.Count == 1 && reusable.Take(gives[0]) is { } id)
                page.Moves.Add(id);
            else
                page.Offers.Add(new GenOffer(localizationId, gives));
        }

        void PlaceFurni(GenPage page, IEnumerable<GenFurni> items)
        {
            foreach (var item in items)
                Place(page, item.Name, [new CatalogProductDraft(item.Type, item.Id, null, 1)]);
        }

        // A builder's plan, its items placed on the page.
        async Task BuildOntoAsync(GenPage page, Func<Plan, Task<string?>> builder, string name)
        {
            var plan = new Plan(name, page.Layout, null);

            await builder(plan).ConfigureAwait(false);

            foreach (var item in plan.Items.Where(x => x.Gives.Count > 0))
                Place(page, item.Item.LocalizationId, item.Gives);
        }

        bool Taken(GenFurni item) => placed.Contains(item.Id);

        // Furni of one kind on pages of at most perPage, numbered when there are more.
        void FurniPages(
            GenPage parent,
            string key,
            string title,
            List<GenFurni> items,
            int? icon = null
        )
        {
            items = [.. items.Where(x => !Taken(x))];

            if (items.Count == 0)
                return;

            var chunks = items.Chunk(perPage).ToList();

            // Spread over three pages or more, a line is a folder of its own, so the pages
            // beside it stay few.
            if (chunks.Count >= 3)
            {
                parent = parent.Child(key, title, icon ?? IconOf(items), DEFAULT_LAYOUT);
                key = "part";
            }

            for (var i = 0; i < chunks.Count; i++)
            {
                var chunk = chunks[i];
                var page = parent.Child(
                    chunks.Count == 1 ? key : $"{key}#{i + 1}",
                    chunks.Count == 1 ? title : $"{title} {i + 1}",
                    icon ?? IconOf(chunk),
                    LayoutOf(chunk)
                );

                PlaceFurni(page, chunk);
            }
        }

        if (sections.Contains(SECTION_FRONT))
        {
            tabs.Add(
                new GenPage(
                    SECTION_FRONT,
                    SECTION_FRONT,
                    "Front Page",
                    Icons.FRONT,
                    AdminCatalogQueries.FRONT_PAGE_LAYOUT
                )
                {
                    Texts = [string.Empty, AdminCatalogQueries.FRONT_PAGE_VOUCHER_TEXT],
                }
            );
        }

        // The special pages come first, so the furni they sell is theirs and not a line's.
        GenPage? furniTab = null;

        if (sections.Contains(SECTION_FURNI))
        {
            furniTab = new GenPage(
                SECTION_FURNI,
                SECTION_FURNI,
                "Furni",
                Icons.FURNI,
                DEFAULT_LAYOUT
            );
            tabs.Add(furniTab);

            await BuildOntoAsync(
                    furniTab.Child(
                        "spaces",
                        "Spaces",
                        Icons.SPACES,
                        CatalogPageBuilders.SPACES_LAYOUT
                    ),
                    p => SpacesAsync(db, p, ct),
                    CatalogPageBuilders.SPACES
                )
                .ConfigureAwait(false);
            await BuildOntoAsync(
                    furniTab.Child(
                        "posters",
                        "Posters",
                        Icons.POSTERS,
                        CatalogPageBuilders.POSTERS_LAYOUT
                    ),
                    p => PostersAsync(db, p, ct),
                    CatalogPageBuilders.POSTERS
                )
                .ConfigureAwait(false);
            await BuildOntoAsync(
                    furniTab.Child(
                        "trophies",
                        "Trophies",
                        Icons.TROPHIES,
                        CatalogPageBuilders.TROPHIES_LAYOUT
                    ),
                    p => TrophiesAsync(db, p, ct),
                    CatalogPageBuilders.TROPHIES
                )
                .ConfigureAwait(false);
            await BuildOntoAsync(
                    furniTab.Child(
                        "badge-displays",
                        "Badge displays",
                        Icons.BADGE_DISPLAYS,
                        CatalogPageBuilders.BADGE_DISPLAYS_LAYOUT
                    ),
                    p => BadgeDisplaysAsync(db, p, ct),
                    CatalogPageBuilders.BADGE_DISPLAYS
                )
                .ConfigureAwait(false);
        }

        if (sections.Contains(SECTION_PETS))
        {
            var tab = new GenPage(SECTION_PETS, SECTION_PETS, "Pets", Icons.PETS, "pets2")
            {
                Texts = [string.Empty, "Pick a pet from the list, name it and take it home."],
            };
            var plan = new Plan(CatalogPageBuilders.PETS, CatalogPageBuilders.PETS_LAYOUT, null);

            await PetsAsync(db, plan, ct).ConfigureAwait(false);

            foreach (var item in plan.Items)
            {
                var page = tab.Child(
                    item.Item.Key.Replace(':', '-'),
                    item.Item.PageTitle ?? item.Item.Title,
                    Icons.PETS,
                    CatalogPageBuilders.PETS_LAYOUT
                );

                Place(page, item.Item.LocalizationId, item.Gives);
            }

            var accessories = tab.Child(
                "accessories",
                "Pet accessories",
                Icons.PET_ACCESSORIES,
                CatalogPageBuilders.PET_CUSTOMIZATION_LAYOUT
            );

            accessories.Name = Named("pet_accessories");
            await BuildOntoAsync(
                    accessories,
                    p => PetCustomizationAsync(db, p, null, ct),
                    CatalogPageBuilders.PET_CUSTOMIZATION
                )
                .ConfigureAwait(false);
            FurniPages(
                tab,
                "care",
                "Pet care",
                [
                    .. furni.Where(x =>
                        string.Equals(x.Category, "pets", StringComparison.OrdinalIgnoreCase)
                        || PetLines.Contains(StemOf(x.Line ?? string.Empty))
                    ),
                ],
                Icons.PET_CARE
            );
            tabs.Add(tab);
        }

        if (sections.Contains(SECTION_WIRED))
        {
            var tab = new GenPage(
                SECTION_WIRED,
                SECTION_WIRED,
                "Wired",
                Icons.WIRED,
                DEFAULT_LAYOUT
            );
            var wired = furni.Where(IsWired).ToList();

            foreach (var (prefix, key, title, icon) in WiredKinds)
                FurniPages(
                    tab,
                    key,
                    title,
                    [
                        .. wired.Where(x =>
                            x.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                        ),
                    ],
                    icon
                );

            FurniPages(tab, "more", "More wired", wired, Icons.WIRED);
            tabs.Add(tab);
        }

        // Taken out of the furni lines whether or not their tab is made.
        foreach (var item in furni.Where(IsWired))
            placed.Add(item.Id);

        if (sections.Contains(SECTION_CLUB))
        {
            var tab = new GenPage(
                SECTION_CLUB,
                SECTION_CLUB,
                "Habbo Club",
                Icons.CLUB,
                AdminCatalogQueries.CLUB_BUY
            )
            {
                Name = Named(AdminCatalogQueries.CLUB_PAGE_NAME),
            };
            var gifts = tab.Child(
                "gifts",
                "Club gifts",
                Icons.CLUB_GIFTS,
                AdminCatalogQueries.CLUB_GIFTS
            );

            gifts.Name = Named(AdminCatalogQueries.CLUB_GIFTS_PAGE_NAME);

            var memberships = offers
                .Where(x => x.Products.Any(p => p.SubscriptionType == SubscriptionType.HabboClub))
                .ToList();

            if (memberships.Count == 0)
                warnings.Add(
                    "No Habbo Club memberships are on offer: add them on the Habbo Club page, which lists every shown membership."
                );

            // The club window and the gifts list read what is shown, so in a fresh catalog they move with it.
            if (replace)
            {
                tab.Moves.AddRange(memberships.Select(x => x.Id));
                gifts.Moves.AddRange(offers.Where(x => x.Gift).Select(x => x.Id));
            }

            tabs.Add(tab);
        }

        if (sections.Contains(SECTION_EXTRAS))
        {
            var tab = new GenPage(
                SECTION_EXTRAS,
                SECTION_EXTRAS,
                "Extras",
                Icons.EXTRAS,
                DEFAULT_LAYOUT
            );
            var effectsPage = tab.Child(
                "effects",
                "Effects",
                Icons.EFFECTS,
                CatalogPageBuilders.EFFECTS_LAYOUT
            );

            effectsPage.Name = Named("avatar_effects");
            await BuildOntoAsync(
                    effectsPage,
                    p => EffectsAsync(db, p, ct),
                    CatalogPageBuilders.EFFECTS
                )
                .ConfigureAwait(false);

            var songs = tab.Child(
                "songs",
                "Trax songs",
                Icons.SONGS,
                CatalogPageBuilders.SONG_DISCS_LAYOUT
            );

            songs.Name = Named("trax_songs");
            await BuildOntoAsync(
                    songs,
                    p => SongDiscsAsync(db, p, ct),
                    CatalogPageBuilders.SONG_DISCS
                )
                .ConfigureAwait(false);

            if (replace)
            {
                var bots = tab.Child("bots", "Bots", Icons.BOTS, "bots");

                bots.Moves.AddRange(
                    offers
                        .Where(x =>
                            !x.Gift
                            && x.Products.Count == 1
                            && x.Products[0].ProductType == ProductType.Robot
                        )
                        .Select(x => x.Id)
                );
            }

            tabs.Add(tab);
        }

        if (sections.Contains(SECTION_RARES))
        {
            var tab = new GenPage(
                SECTION_RARES,
                SECTION_RARES,
                "Rares",
                Icons.RARES,
                DEFAULT_LAYOUT
            );
            var soldOut = limited
                .Where(x =>
                    x.Products.Any(p =>
                        seriesOf.TryGetValue(p.Id, out var s) && s.RemainingQuantity == 0
                    )
                )
                .Select(x => x.Id)
                .ToHashSet();

            if (replace)
                tab.Child("limited", "Limited", Icons.LIMITED, DEFAULT_LAYOUT)
                    .Moves.AddRange(limited.Select(x => x.Id).Where(x => !soldOut.Contains(x)));

            var sold = tab.Child(
                "sold-out",
                "Sold out",
                Icons.SOLD_OUT,
                CatalogPageBuilders.SOLD_LIMITED_LAYOUT
            );

            sold.Name = Named("limited_sold");

            if (replace)
                sold.Moves.AddRange(soldOut);

            foreach (var (line, items) in LinesOf(furni.Where(x => IsRare(x.Line) && !Taken(x))))
                FurniPages(tab, $"line:{line}", Humanize(line), items, Icons.RARES);

            // A rare made new has the everyday price: hidden, to be priced first.
            foreach (var page in tab.Children.Where(x => x.Offers.Count > 0))
                page.Display = CatalogPageDisplay.Invisible;

            if (tab.Children.Any(x => x.Offers.Count > 0))
                warnings.Add(
                    "Rares made new get the everyday price, so their pages start hidden: price them, then show them."
                );

            tabs.Add(tab);
        }

        foreach (var item in furni.Where(x => IsRare(x.Line)))
            placed.Add(item.Id);

        if (sections.Contains(SECTION_BUILDERS))
        {
            var tab = new GenPage(
                SECTION_BUILDERS,
                SECTION_BUILDERS,
                "Builders Club",
                Icons.BUILDERS,
                "builders_club_frontpage"
            );

            if (replace)
                tab.Moves.AddRange(
                    offers
                        .Where(x =>
                            x.Products.Any(p => p.SubscriptionType == SubscriptionType.BuildersClub)
                        )
                        .Select(x => x.Id)
                );

            foreach (
                var (line, items) in LinesOf(furni.Where(x => IsBuildersClub(x.Line) && !Taken(x)))
            )
                FurniPages(tab, $"line:{line}", Humanize(line), items, Icons.BUILDERS);

            tab.Child("addons", "Add-ons", Icons.BUILDERS, "builders_club_addons");

            // The Builders Club catalog has no tabs: its pages are under one the normal catalog shows.
            foreach (var page in tab.Children)
                page.Display = CatalogPageDisplay.BuildersClubOnly;

            tabs.Add(tab);
        }

        foreach (var item in furni.Where(x => IsBuildersClub(x.Line)))
            placed.Add(item.Id);

        if (sections.Contains(SECTION_GROUPS))
        {
            var tab = new GenPage(
                SECTION_GROUPS,
                SECTION_GROUPS,
                "Groups",
                Icons.GROUPS,
                "guild_frontpage"
            )
            {
                Texts =
                [
                    string.Empty,
                    "Start a group: pick its badge and colours, and give it a home room.",
                ],
            };
            var custom = tab.Child("furni", "Group furni", Icons.GROUPS, "guild_custom_furni");

            custom.Name = Named("guild_custom_furni");
            PlaceFurni(
                custom,
                furni.Where(x => x.Logic == GuildFurnitureLogicNames.CUSTOMIZED && !Taken(x))
            );

            var forums = furni
                .Where(x => x.Logic == GuildFurnitureLogicNames.FORUM && !Taken(x))
                .ToList();

            if (forums.Count > 0)
                PlaceFurni(
                    tab.Child("forums", "Group forums", Icons.GROUPS, "guild_forum"),
                    forums
                );

            tabs.Add(tab);
        }

        foreach (
            var item in furni.Where(x =>
                x.Logic is GuildFurnitureLogicNames.CUSTOMIZED or GuildFurnitureLogicNames.FORUM
            )
        )
            placed.Add(item.Id);

        // What is left is the furni: each line a page (a long one, numbered pages), a family of
        // lines a folder, the lines and families by theme, and the furni in no line by kind.
        if (furniTab is not null)
        {
            var rest = furni.Where(x => !Taken(x)).ToList();
            var lined = rest.Where(x => !string.IsNullOrWhiteSpace(x.Line)).ToList();
            var themed = new Dictionary<string, List<(string Title, Action<GenPage> Add)>>(
                StringComparer.Ordinal
            );

            foreach (var (family, lines) in FamiliesOf(lined))
            {
                var theme = ThemeOf(family);
                var entries = themed.TryGetValue(theme.Key, out var list)
                    ? list
                    : themed[theme.Key] = [];

                if (lines.Count == 1)
                {
                    var (line, items) = lines[0];

                    entries.Add(
                        (
                            Humanize(line),
                            parent => FurniPages(parent, $"line:{line}", Humanize(line), items)
                        )
                    );

                    continue;
                }

                var title = Humanize(family);

                entries.Add(
                    (
                        title,
                        parent =>
                        {
                            var folder = parent.Child(
                                $"family:{family}",
                                title,
                                FamilyIcons.GetValueOrDefault(
                                    family,
                                    IconOf([.. lines.SelectMany(x => x.Items)])
                                ),
                                DEFAULT_LAYOUT
                            );

                            foreach (var (line, items) in lines)
                                FurniPages(folder, $"line:{line}", Humanize(line), items);
                        }
                    )
                );
            }

            foreach (var theme in Themes.Append(OtherLines))
            {
                if (!themed.TryGetValue(theme.Key, out var entries))
                    continue;

                var folder = furniTab.Child(
                    $"theme:{theme.Key}",
                    theme.Title,
                    theme.Icon,
                    DEFAULT_LAYOUT
                );
                var sorted = entries
                    .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // A theme with more than a folder takes in at a glance is split by letter.
                if (sorted.Count <= FOLDER_MAX)
                {
                    foreach (var (_, add) in sorted)
                        add(folder);

                    continue;
                }

                foreach (var range in LetterRanges(sorted, x => x.Title))
                {
                    var part = folder.Child(
                        $"letters:{range.Label}",
                        $"{theme.Title} {range.Label}",
                        theme.Icon,
                        DEFAULT_LAYOUT
                    );

                    foreach (var (_, add) in range.Items)
                        add(part);
                }
            }

            var unlined = rest.Where(x => string.IsNullOrWhiteSpace(x.Line) && !Taken(x)).ToList();

            if (unlined.Count > 0)
            {
                var more = furniTab.Child("more", "More furni", Icons.FURNI, DEFAULT_LAYOUT);

                foreach (
                    var kind in unlined
                        .GroupBy(x =>
                            Categories.ContainsKey(x.Category ?? string.Empty)
                                ? x.Category!.ToLowerInvariant()
                                : string.Empty
                        )
                        .OrderBy(x => x.Key.Length == 0)
                        .ThenBy(x => x.Key, StringComparer.Ordinal)
                )
                {
                    var (title, icon) = Categories.GetValueOrDefault(
                        kind.Key,
                        ("Other", Icons.FURNI)
                    );

                    FurniPages(
                        more,
                        $"kind:{(kind.Key.Length > 0 ? kind.Key : "other")}",
                        title,
                        [.. kind],
                        icon
                    );
                }
            }
        }

        // A page left with nothing to sell and nothing under it is not made, but the pages that
        // sell from elsewhere: the front page, the club shop and gifts, the Builders Club's own.
        foreach (var tab in tabs)
            Prune(tab);

        tabs.RemoveAll(x => x.Children.Count == 0 && x.IsEmpty && !SellsFromElsewhere(x));

        // The tabs in the order players meet them, whatever order they were worked out in.
        tabs.Sort(
            (x, y) =>
                Array
                    .IndexOf(GenerateSections, x.Section)
                    .CompareTo(Array.IndexOf(GenerateSections, y.Section))
        );
        ApplyEdits(tabs, request.Edits);

        var archive = replace
            ? pages.Where(x => x.ParentEntityId == rootId).Select(x => x.Id).ToList()
            : [];

        if (replace && archive.Count > 0)
            warnings.Add(
                $"The {archive.Count} tabs there now go, with everything under them, into a hidden tab, {ARCHIVE_TITLE}: nothing is deleted, and a link by a page's name still opens it."
            );

        if (!replace)
        {
            foreach (var tab in tabs)
                tab.Display = CatalogPageDisplay.Invisible;

            warnings.Add(
                "The new tabs start hidden, after the old ones: show them, and hide the old, when they're ready."
            );
        }

        var made = tabs.Sum(x => x.NewOffers);

        if (made > 0)
            warnings.Add(
                $"{made:N0} offers are made new at {PriceOf(request)} each; reprice pages afterwards where they should cost more."
            );

        return new Generated(request, replace, rootId, tabs, archive, warnings);
    }

    private static string PriceOf(CatalogGenerateRequest request) =>
        request.CostCredits == 0 && request.CostCurrency == 0 ? "no cost"
        : request.CostCurrency == 0 ? $"{request.CostCredits} credits"
        : $"{request.CostCredits} credits and {request.CostCurrency} points";

    private static bool SellsFromElsewhere(GenPage page) =>
        page.Layout
            is AdminCatalogQueries.FRONT_PAGE_LAYOUT
                or AdminCatalogQueries.CLUB_BUY
                or AdminCatalogQueries.CLUB_GIFTS
                or "builders_club_frontpage"
                or "builders_club_addons"
                or "guild_frontpage";

    /// <summary>Takes away the pages under a page that would sell nothing and hold nothing.</summary>
    private static void Prune(GenPage page)
    {
        foreach (var child in page.Children)
            Prune(child);

        page.Children.RemoveAll(x => x.Children.Count == 0 && x.IsEmpty && !SellsFromElsewhere(x));
    }

    /// <summary>The editor's changes to the plan: a page retitled, given another icon, or left out with all under it.</summary>
    private static void ApplyEdits(List<GenPage> pages, CatalogGeneratePageEdit[]? edits)
    {
        if (edits is null || edits.Length == 0)
            return;

        var byKey = edits
            .Where(x => x.Key is not null)
            .GroupBy(x => x.Key)
            .ToDictionary(g => g.Key, g => g.Last());

        void Walk(List<GenPage> list)
        {
            list.RemoveAll(x => byKey.TryGetValue(x.Key, out var edit) && edit.Skip);

            foreach (var page in list)
            {
                if (byKey.TryGetValue(page.Key, out var edit))
                {
                    if (edit.Title?.Trim() is { Length: > 0 and <= TITLE_MAX } title)
                        page.Title = title;

                    if (edit.Icon is >= 0 and var icon)
                        page.Icon = icon;
                }

                Walk(page.Children);
            }
        }

        Walk(pages);
    }

    /// <summary>The most a furni folder lists before it is split by letter.</summary>
    private const int FOLDER_MAX = 24;

    /// <summary>A shelf of the furni tab: furni lines that belong together, by the word they start with.</summary>
    private sealed record Theme(string Key, string Title, int Icon, string[] Stems);

    /// <summary>
    /// The furni tab's themes, in its order, each with the words its lines' names start with, as
    /// Habbo names its official lines. A line no theme claims is under <see cref="OtherLines"/>.
    /// </summary>
    private static readonly Theme[] Themes =
    [
        new(
            "seasonal",
            "Seasonal",
            Icons.CHRISTMAS,
            [
                "xmas",
                "christmas",
                "habboween",
                "hween",
                "easter",
                "valentine",
                "val",
                "newyear",
                "summer",
                "winter",
                "wintercabin",
                "winterhorizon",
                "fall",
                "cny",
                "st",
                "lunar",
            ]
        ),
        new(
            "classic",
            "Classic lines",
            14,
            [
                "plasto",
                "pura",
                "mode",
                "iced",
                "area",
                "diner",
                "bazaar",
                "classics",
                "country",
                "lodge",
                "glass",
                "executive",
                "elegant",
                "romantique",
                "modern",
                "dark",
                "darkelegant",
                "antique",
                "anna",
                "gold",
                "bling",
                "diamond",
                "rugs",
                "windows",
                "hygge",
                "kuurna",
                "usva",
                "waasa",
            ]
        ),
        new(
            "world",
            "Around the world",
            16,
            [
                "african",
                "alhambra",
                "america",
                "asian",
                "brazil",
                "india",
                "japan",
                "tokyo",
                "mexico",
                "paris",
                "london",
                "nyc",
                "santorini",
                "skorea",
                "thailand",
                "greek",
                "olympus",
                "shalimar",
                "vikings",
                "flags",
                "ancients",
                "lost",
                "wildwest",
                "pirates",
                "tiki",
                "exotic",
                "egypt",
                "china",
            ]
        ),
        new(
            "fantasy",
            "Fantasy & sci-fi",
            109,
            [
                "fantasy",
                "mystics",
                "celestial",
                "stellar",
                "scifi",
                "cyberpunk",
                "neon",
                "neonpunk",
                "magitech",
                "steampunk",
                "gothic",
                "gothiccafe",
                "voodoo",
                "dino",
                "drago",
                "dream",
                "wonderland",
                "monsterplant",
                "mushroom",
                "virus",
                "survival",
                "mad",
                "coral",
                "arctic",
                "vaporwave",
            ]
        ),
        new(
            "rooms",
            "Rooms & places",
            15,
            [
                "baths",
                "spa",
                "kitchen",
                "household",
                "hotel",
                "school",
                "university",
                "supermarket",
                "mall",
                "cinema",
                "cat",
                "dessertcafe",
                "icecream",
                "sunsetcafe",
                "boutique",
                "runway",
                "attic",
                "nt",
                "newbie",
                "room",
                "relax",
                "messy",
                "mafia",
                "jetset",
                "suncity",
                "urban",
                "graffiti",
                "traffic",
                "rainyday",
                "pj",
                "army",
                "art",
                "miniatures",
                "hobbies",
                "display",
                "social",
            ]
        ),
        new(
            "nature",
            "Nature & animals",
            45,
            [
                "garden",
                "plants",
                "jungle",
                "ranch",
                "birds",
                "penguins",
                "fruit",
                "coco",
                "organo",
                "nature",
            ]
        ),
        new(
            "music",
            "Music & parties",
            4,
            [
                "disco",
                "band",
                "trax",
                "traxpax",
                "sfx",
                "habbopalooza",
                "festival",
                "circus",
                "habbowood",
            ]
        ),
        new(
            "games",
            "Games & sports",
            56,
            [
                "arcade",
                "chess",
                "football",
                "freeze",
                "sport",
                "snowwar",
                "snowboard",
                "habbolympics",
                "olympics",
                "puzzle",
                "cars",
                "auto",
                "gacha",
            ]
        ),
        new(
            "cute",
            "Cute & colourful",
            107,
            [
                "candyland",
                "sanrio",
                "smiley",
                "rainbow",
                "pastel",
                "plushie",
                "bubblejuice",
                "cubie",
            ]
        ),
        new(
            "collectibles",
            "Collectibles",
            Icons.NFT,
            [
                "nft",
                "nftmint",
                "nftmerch",
                "collectibles",
                "pixel",
                "merch",
                "legacy",
                "rewardtrack",
            ]
        ),
        new(
            "specials",
            "Hotel specials",
            6,
            [
                "habbo",
                "hhistory",
                "credit",
                "duckets",
                "loyalty",
                "club",
                "old",
                "gifts",
                "presents",
                "recycler",
                "ecotron",
                "furnimatic",
                "rentables",
                "booster",
                "ad",
                "stories",
                "user",
                "viral",
                "ua",
                "ui",
                "configuration",
                "misc",
                "extras",
                "tablet",
                "background",
                "spaces",
                "fxbox",
                "clothing",
                "guilds",
                "origins",
                "trophies",
            ]
        ),
    ];

    /// <summary>Where the lines no theme claims go.</summary>
    private static readonly Theme OtherLines = new("other", "More lines", Icons.FURNI, []);

    /// <summary>Lines of pet furni, which go with pet care on the pets tab.</summary>
    private static readonly HashSet<string> PetLines = new(StringComparer.OrdinalIgnoreCase)
    {
        "pet",
        "nests",
        "horse",
    };

    private static Theme ThemeOf(string stem) =>
        Themes.FirstOrDefault(x => x.Stems.Contains(stem, StringComparer.OrdinalIgnoreCase))
        ?? OtherLines;

    /// <summary>
    /// A sorted list cut into parts of at most <see cref="FOLDER_MAX"/>, as even as they come,
    /// each named by the letters it runs from and to (<c>A–F</c>).
    /// </summary>
    private static List<(string Label, List<T> Items)> LetterRanges<T>(
        List<T> sorted,
        Func<T, string> title
    )
    {
        var parts = (sorted.Count + FOLDER_MAX - 1) / FOLDER_MAX;
        var size = (sorted.Count + parts - 1) / parts;

        return
        [
            .. sorted
                .Chunk(size)
                .Select(chunk =>
                {
                    var first = char.ToUpperInvariant(title(chunk[0])[0]);
                    var last = char.ToUpperInvariant(title(chunk[^1])[0]);

                    return (first == last ? $"{first}" : $"{first}\u2013{last}", chunk.ToList());
                }),
        ];
    }

    private static bool IsWired(GenFurni item) =>
        item.Name.StartsWith(WIRED_PREFIX, StringComparison.OrdinalIgnoreCase)
        || string.Equals(item.Line, "wired", StringComparison.OrdinalIgnoreCase)
        || (item.Category?.StartsWith("wired", StringComparison.OrdinalIgnoreCase) ?? false);

    private static bool IsRare(string? line) =>
        line is { } name
        && (
            name.Equals("rare", StringComparison.OrdinalIgnoreCase)
            || name.Equals("bonusrare", StringComparison.OrdinalIgnoreCase)
            || name.Contains("ltd", StringComparison.OrdinalIgnoreCase)
            || name.Contains("limited", StringComparison.OrdinalIgnoreCase)
        );

    private static bool IsBuildersClub(string? line) =>
        line is { } name
        && (
            name.StartsWith("buildersclub", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("bc_", StringComparison.OrdinalIgnoreCase)
        );

    /// <summary>The furni by line, the lines by name.</summary>
    private static List<(string Line, List<GenFurni> Items)> LinesOf(IEnumerable<GenFurni> items) =>
        [
            .. items
                .GroupBy(x => x.Line!.Trim(), StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => (g.Key, g.ToList())),
        ];

    /// <summary>
    /// The lines by family: those whose names start with the same word (<c>xmas2018</c> and
    /// <c>xmas2023</c>) are one family when they are enough to be one; a line on its own is a
    /// family of one.
    /// </summary>
    private static List<(
        string Family,
        List<(string Line, List<GenFurni> Items)> Lines
    )> FamiliesOf(IEnumerable<GenFurni> items) =>
        [
            .. LinesOf(items)
                .GroupBy(x => StemOf(x.Line), StringComparer.OrdinalIgnoreCase)
                .SelectMany(g =>
                    IsFamily([.. g.Select(x => x.Line)])
                        ? [(g.Key, g.ToList())]
                        : g.Select(x => (StemOf(x.Line), new List<(string, List<GenFurni>)> { x }))
                ),
        ];

    /// <summary>
    /// Whether lines that start with one word are a family: three or more, or lines told apart
    /// by year (<c>garden16</c>, <c>garden18</c>). Two lines that merely share a word
    /// (<c>mad_money</c>, <c>mad_scientist</c>) are not.
    /// </summary>
    private static bool IsFamily(List<string> lines) =>
        lines.Count >= 3 || (lines.Count == 2 && lines.Any(x => x.Any(char.IsAsciiDigit)));

    private static string StemOf(string line) =>
        StemPattern().Match(line) is { Success: true } match
            ? match.Groups[1].Value.ToLowerInvariant()
            : line.ToLowerInvariant();

    /// <summary>A furni line's name as a page title: <c>xmas2018</c> is Christmas 2018, <c>iced_dark</c> Iced Dark.</summary>
    private static string Humanize(string line)
    {
        var words = WordPattern()
            .Matches(line)
            .Select(x =>
                Words.TryGetValue(x.Value, out var word)
                    ? word
                    : char.ToUpperInvariant(x.Value[0]) + x.Value[1..].ToLowerInvariant()
            )
            .ToList();
        var title = words.Count > 0 ? string.Join(' ', words) : line;

        return title.Length > TITLE_MAX ? title[..TITLE_MAX] : title;
    }

    /// <summary>A page title's longest, as the edit service takes it.</summary>
    private const int TITLE_MAX = 50;

    /// <summary>The icon of the kind most of a page's furni are, by furnidata category.</summary>
    private static int IconOf(IReadOnlyCollection<GenFurni> items) =>
        items
            .Select(x => x.Category ?? string.Empty)
            .Where(x => Categories.ContainsKey(x))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Count())
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .Select(x => Categories[x.Key].Icon)
            .FirstOrDefault(Icons.FURNI);

    /// <summary>The colour-grouped grid for a page mostly of colours of a few furni (<c>chair*1</c>, <c>chair*2</c>), else the shop grid.</summary>
    private static string LayoutOf(IReadOnlyCollection<GenFurni> items) =>
        items.Count(x => x.Name.Contains(COLOUR_SEPARATOR)) * 4 >= items.Count
            ? CatalogPageBuilders.COLOURS_LAYOUT
            : DEFAULT_LAYOUT;

    [GeneratedRegex("^([A-Za-z]+?)(?:_|[0-9]|$)")]
    private static partial Regex StemPattern();

    [GeneratedRegex("[A-Za-z]+|[0-9]+")]
    private static partial Regex WordPattern();
}
