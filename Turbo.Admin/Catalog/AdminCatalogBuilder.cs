using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Furniture;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Pets;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Catalog;

/// <summary>
/// The catalog editor's page builders: each fills a page with the offers a kind of page sells
/// (every trophy, every pet, a colour family, a furni line, the pet customisation items, the
/// avatar effects, the sold-out limited offers), named the way that page's layout reads them.
/// A plan is worked out from the furniture, the product data and the catalog as saved; applying
/// works it out again and makes the chosen items through <see cref="ICatalogEditService"/>, so
/// every check an edit by hand gets applies, and an item it refuses is reported and skipped.
/// </summary>
public sealed partial class AdminCatalogBuilder(
    IDbContextFactory<TurboDbContext> database,
    ICatalogEditService editor,
    IGivableEffects effects,
    IOptions<AdminConfig> config
)
{
    /// <summary>The shortest class-name prefix the furni line builder takes; shorter matches most of the hotel.</summary>
    public const int PREFIX_MIN_LENGTH = 3;

    /// <summary>What a Habbo product data code starts with before the trophy or pet it names.</summary>
    private const string PRODUCT_CODE_PREFIX = "a0 ";

    /// <summary>The product data code of an effect, before its id.</summary>
    private const string EFFECT_PRODUCT_PREFIX = "avatar_effect";

    /// <summary>The external text of an effect's name, before its id.</summary>
    private const string EFFECT_TEXT_PREFIX = "fx_";

    /// <summary>The external text of a pet type's name, before its id.</summary>
    private const string PET_TYPE_TEXT_PREFIX = "pet.type.";

    /// <summary>What Habbo's pet product names end with, which a page title leaves off.</summary>
    private const string STARTER_FOOD_SUFFIX = " and starter food";

    /// <summary>What separates a colour family's class name from the colour (<c>chair_plasty*4</c>).</summary>
    private const char COLOUR_SEPARATOR = '*';

    private const string NO_PRODUCT_NAME = "no product data name";

    /// <summary>The failure key of setting the page's layout, which is no plan item.</summary>
    private const string LAYOUT_KEY = "layout";

    /// <summary>
    /// A trophy family's first three colours, as the trophies window groups them: by the offer
    /// name's ending, gold, silver and bronze.
    /// </summary>
    private static readonly (string Suffix, string Colour)[] TrophyColours =
    [
        ("g", "gold"),
        ("s", "silver"),
        ("b", "bronze"),
    ];

    private static readonly string[] BuilderNames =
    [
        CatalogPageBuilders.TROPHIES,
        CatalogPageBuilders.PETS,
        CatalogPageBuilders.COLOURS,
        CatalogPageBuilders.FURNI_LINE,
        CatalogPageBuilders.PET_CUSTOMIZATION,
        CatalogPageBuilders.EFFECTS,
        CatalogPageBuilders.SOLD_LIMITED,
        CatalogPageBuilders.SPACES,
        CatalogPageBuilders.POSTERS,
        CatalogPageBuilders.BADGE_DISPLAYS,
        CatalogPageBuilders.SONG_DISCS,
    ];

    /// <summary>
    /// The room papers, in the order the spaces page lists its groups: the product data code a
    /// pattern is sold under starts with the paper's class name and <c>_single_</c>, and every
    /// pattern of a paper is that one definition with the pattern as its extra parameter.
    /// </summary>
    private static readonly (string Name, string CodePrefix)[] RoomPapers =
    [
        ("floor", "floor_single_"),
        ("wallpaper", "wallpaper_single_"),
        ("landscape", "landscape_single_"),
    ];

    /// <summary>The one definition every poster is.</summary>
    private const string POSTER_NAME = "poster";

    /// <summary>What a poster's product data code starts with, before its id.</summary>
    private const string POSTER_CODE_PREFIX = "poster ";

    private const string NEEDS_PATTERN =
        "Spaces and posters need a pattern: use the Spaces or Posters builder.";

    private const string NEEDS_SONG = "Song discs need a song: use the Song discs builder.";

    /// <summary>
    /// What Habbo's product data names a song disk offer by, before the official song's code
    /// (<c>SONG DubStep1</c>).
    /// </summary>
    private const string SONG_PRODUCT_PREFIX = "SONG ";

    /// <summary>The class name of the song disk, preferred when several items are disks.</summary>
    private const string SONG_DISK_NAME = "song_disk";

    /// <summary>Every furni line the furniture is in, with how many definitions each has, by name.</summary>
    public async Task<CatalogFurniLinesResponse> GetFurniLinesAsync(CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var lines = await db
            .FurnitureDefinitions.AsNoTracking()
            .Where(x => x.FurniLine != null && x.FurniLine != string.Empty)
            .GroupBy(x => x.FurniLine!)
            .Select(g => new CatalogFurniLine(g.Key, g.Count()))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new([.. lines.OrderBy(x => x.Line, StringComparer.Ordinal)]);
    }

    /// <summary>What the builder would put on the page, up to the preview's limit; nothing is changed.</summary>
    public async Task<CatalogBuildOutcome<CatalogBuildPlan>> PreviewAsync(
        int pageId,
        CatalogBuildRequest request,
        CancellationToken ct
    )
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var plan = await PlanAsync(db, pageId, request, ct).ConfigureAwait(false);

        if (plan.Error is { } error)
            return CatalogBuildOutcome<CatalogBuildPlan>.Refused(error);

        var limit = Math.Max(1, config.Value.CatalogBuilderItemLimit);
        var warnings = plan.Warnings;

        if (plan.Items.Count > limit)
            warnings.Add(
                $"Showing the first {limit} of {plan.Items.Count}; narrow it down to see the rest."
            );

        return CatalogBuildOutcome<CatalogBuildPlan>.Done(
            new CatalogBuildPlan(
                plan.Builder,
                plan.Layout,
                plan.Builder == CatalogPageBuilders.PETS,
                [.. plan.Items.Take(limit).Select(x => x.Item)],
                [.. warnings]
            )
        );
    }

    /// <summary>
    /// Makes the plan's items named by <see cref="CatalogBuildRequest.Keys"/>, in the plan's
    /// order: an offer on the page each, a page under it with its offer for each pet, or the
    /// sold-out limited offer moved onto it. The plan is worked out afresh, so what an item gives
    /// is the hotel's, never the panel's.
    /// </summary>
    public async Task<CatalogBuildOutcome<CatalogBuildResponse>> ApplyAsync(
        PlayerId editorId,
        int pageId,
        CatalogBuildRequest request,
        CancellationToken ct
    )
    {
        var childDisplay = CatalogPageDisplay.Invisible;

        if (request.Display is { } displayName)
        {
            if (CatalogPageDisplayExtensions.FromName(displayName) is not { } display)
                return CatalogBuildOutcome<CatalogBuildResponse>.Refused(
                    "A page is shown in the regular catalog, the Builders Club catalog, both, or neither."
                );

            childDisplay = display;
        }

        Plan plan;
        CatalogPageEntity? page;
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);

        await using (db.ConfigureAwait(false))
        {
            plan = await PlanAsync(db, pageId, request, ct).ConfigureAwait(false);
            page = plan.Page;
        }

        if (plan.Error is { } error || page is null)
            return CatalogBuildOutcome<CatalogBuildResponse>.Refused(plan.Error ?? "Not built.");

        var wanted = new HashSet<string>(request.Keys ?? [], StringComparer.Ordinal);

        if (wanted.Count == 0 && !request.SetLayout)
            return CatalogBuildOutcome<CatalogBuildResponse>.Refused("Choose the items to build.");

        var known = plan.Items.Select(x => x.Item.Key).ToHashSet(StringComparer.Ordinal);
        var failures = new List<CatalogBuildFailure>(
            (request.Keys ?? [])
                .Where(x => !known.Contains(x))
                .Distinct(StringComparer.Ordinal)
                .Select(x => new CatalogBuildFailure(x, "That is not in the plan any more."))
        );
        var offersCreated = 0;
        var pagesCreated = 0;
        var offersMoved = 0;

        foreach (var item in plan.Items.Where(x => wanted.Contains(x.Item.Key)))
        {
            var key = item.Item.Key;

            if (plan.Builder == CatalogPageBuilders.SOLD_LIMITED)
            {
                var moved = await editor
                    .MoveOfferAsync(editorId, item.Item.OfferId!.Value, pageId, int.MaxValue, ct)
                    .ConfigureAwait(false);

                if (moved.Saved)
                    offersMoved++;
                else
                    failures.Add(Failure(key, moved));

                continue;
            }

            var offerPageId = pageId;

            if (plan.Builder == CatalogPageBuilders.PETS)
            {
                var created = await editor
                    .CreatePageAsync(
                        editorId,
                        pageId,
                        new CatalogPageDraft(
                            item.Item.PageTitle ?? item.Item.Title,
                            null,
                            page.Icon,
                            CatalogPageBuilders.PETS_LAYOUT,
                            [],
                            [],
                            childDisplay
                        ),
                        ct
                    )
                    .ConfigureAwait(false);

                if (!created.Saved)
                {
                    failures.Add(Failure(key, created));

                    continue;
                }

                offerPageId = created.Id;
            }

            var offer = await editor
                .CreateOfferAsync(editorId, OfferDraft(offerPageId, item, request), ct)
                .ConfigureAwait(false);

            if (!offer.Saved)
            {
                failures.Add(Failure(key, offer));

                // A pet page is its one offer; without it the page would show nothing.
                if (offerPageId != pageId)
                    await editor.DeletePageAsync(editorId, offerPageId, ct).ConfigureAwait(false);

                continue;
            }

            offersCreated++;

            if (offerPageId != pageId)
                pagesCreated++;
        }

        // The pet builder's pages carry the layout; the page they are under keeps its own.
        if (request.SetLayout && plan.Builder != CatalogPageBuilders.PETS)
        {
            var saved = await editor
                .UpdatePageAsync(
                    editorId,
                    pageId,
                    new CatalogPageDraft(
                        page.Localization,
                        page.Name,
                        page.Icon,
                        plan.Layout,
                        page.ImageData ?? [],
                        page.TextData ?? [],
                        page.Display
                    ),
                    ct
                )
                .ConfigureAwait(false);

            if (!saved.Saved)
                failures.Add(Failure(LAYOUT_KEY, saved));
        }

        return CatalogBuildOutcome<CatalogBuildResponse>.Done(
            new CatalogBuildResponse(
                offersCreated,
                pagesCreated,
                offersMoved,
                editor.UnpublishedChanges,
                [.. failures]
            )
        );
    }

    private static CatalogBuildFailure Failure(string key, CatalogEditResult result) =>
        new(key, result.Error ?? "Not saved.");

    /// <summary>A new offer of a plan item: what the plan gives, at the price and flags asked for.</summary>
    private static CatalogOfferDraft OfferDraft(
        int pageId,
        PlanItem item,
        CatalogBuildRequest request
    ) =>
        new(
            pageId,
            item.Item.LocalizationId,
            request.CostCredits,
            request.CostCurrency,
            request.CurrencyTypeId,
            request.CanGift,
            // Bought in bulk where it can be; the service turns it off for what is given once.
            CanBundle: true,
            request.ClubLevel,
            request.Visible,
            Product: null,
            ClubGiftDaysRequired: null,
            Products: item.Gives
        );

    /// <summary>The builder's plan for the page, or why there is none.</summary>
    private async Task<Plan> PlanAsync(
        TurboDbContext db,
        int pageId,
        CatalogBuildRequest request,
        CancellationToken ct
    )
    {
        var builder = BuilderNames.FirstOrDefault(x =>
            string.Equals(x, request.Builder?.Trim(), StringComparison.OrdinalIgnoreCase)
        );

        if (builder is null)
            return Plan.Refused($"Choose a builder: {string.Join(", ", BuilderNames)}.");

        var page = await db
            .CatalogPages.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == pageId, ct)
            .ConfigureAwait(false);

        if (page is null)
            return Plan.Refused("That page is gone.");

        var plan = new Plan(builder, CatalogPageBuilders.LayoutOf(builder)!, page);
        var error = builder switch
        {
            CatalogPageBuilders.TROPHIES => await TrophiesAsync(db, plan, ct).ConfigureAwait(false),
            CatalogPageBuilders.PETS => await PetsAsync(db, plan, ct).ConfigureAwait(false),
            CatalogPageBuilders.COLOURS => await ColoursAsync(db, plan, request.Base, ct)
                .ConfigureAwait(false),
            CatalogPageBuilders.FURNI_LINE => await FurniLineAsync(db, plan, request, ct)
                .ConfigureAwait(false),
            CatalogPageBuilders.PET_CUSTOMIZATION => await PetCustomizationAsync(
                    db,
                    plan,
                    request.PetType,
                    ct
                )
                .ConfigureAwait(false),
            CatalogPageBuilders.EFFECTS => await EffectsAsync(db, plan, ct).ConfigureAwait(false),
            CatalogPageBuilders.SPACES => await SpacesAsync(db, plan, ct).ConfigureAwait(false),
            CatalogPageBuilders.POSTERS => await PostersAsync(db, plan, ct).ConfigureAwait(false),
            CatalogPageBuilders.BADGE_DISPLAYS => await BadgeDisplaysAsync(db, plan, ct)
                .ConfigureAwait(false),
            CatalogPageBuilders.SONG_DISCS => await SongDiscsAsync(db, plan, ct)
                .ConfigureAwait(false),
            _ => await SoldLimitedAsync(db, plan, pageId, ct).ConfigureAwait(false),
        };

        if (error is not null)
            return Plan.Refused(error);

        if (plan.Items.Count == 0)
            plan.Warnings.Add("There is nothing to build.");

        return plan;
    }

    /// <summary>
    /// Every trophy. A family's first three colours (<c>prizetrophy2*1</c> to <c>*3</c>) are
    /// named as Habbo names them, <c>a0 prizetrophy2_g</c>, <c>_s</c> and <c>_b</c>, so the
    /// trophies window groups them as one trophy in gold, silver and bronze; without that
    /// product data, the same name without <c>a0</c>. Any other trophy is an offer of its own.
    /// </summary>
    private async Task<string?> TrophiesAsync(TurboDbContext db, Plan plan, CancellationToken ct)
    {
        var trophies = await db
            .FurnitureDefinitions.AsNoTracking()
            .Where(x => x.Logic == TrophyData.LOGIC_NAME && x.ProductType == ProductType.Floor)
            .Select(AsDefinition)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var members = trophies
            .Select(x => (Definition: x, Family: Family(x.Name)))
            .OrderBy(x => x.Family.Base, StringComparer.Ordinal)
            .ThenBy(x => x.Family.Colour ?? 0)
            .ThenBy(x => x.Definition.Name, StringComparer.Ordinal)
            .ToList();
        var codes = members.Select(x => TrophyCode(x.Definition.Name, x.Family)).ToList();
        var names = await ProductNamesAsync(db, codes, ct).ConfigureAwait(false);
        var sold = await SoldDefinitionsAsync(db, ct).ConfigureAwait(false);

        for (var i = 0; i < members.Count; i++)
        {
            var (definition, family) = members[i];
            var code = codes[i];
            var named = names.TryGetValue(code, out var productName);
            (string Suffix, string Colour)? colour = family.Colour is { } n and >= 1 and <= 3
                ? TrophyColours[n - 1]
                : null;
            var key = colour is { } grouped
                ? $"trophy:{family.Base}:{grouped.Suffix}"
                : $"def:{Number(definition.Id)}";
            var localizationId =
                named ? code
                : colour is { } plain ? $"{family.Base}_{plain.Suffix}"
                : definition.Name;

            plan.Items.Add(
                FurniItem(key, localizationId, definition, productName, sold, colour?.Colour)
            );
        }

        return null;
    }

    /// <summary>The product data code a trophy would be sold under, as Habbo names it.</summary>
    private static string TrophyCode(string name, (string Base, int? Colour) family) =>
        family.Colour is { } n and >= 1 and <= 3
            ? $"{PRODUCT_CODE_PREFIX}{family.Base}_{TrophyColours[n - 1].Suffix}"
            : $"{PRODUCT_CODE_PREFIX}{name}";

    /// <summary>
    /// Every pet type with a palette a buyer can choose, an offer each on a page of its own: the
    /// pet window reads the type from the digits that end the offer's name, <c>a0 pet12</c>,
    /// and shows only a page's first offer.
    /// </summary>
    private static async Task<string?> PetsAsync(TurboDbContext db, Plan plan, CancellationToken ct)
    {
        var types = await db
            .PetBreeds.AsNoTracking()
            .Where(x => x.Sellable)
            .Select(x => x.TypeId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var codes = types.Select(x => PRODUCT_CODE_PREFIX + PetProductCodes.ForTypeId(x)).ToList();
        var names = await ProductNamesAsync(db, codes, ct).ConfigureAwait(false);
        var texts = await TextsAsync(
                db,
                [.. types.Select(x => PET_TYPE_TEXT_PREFIX + Number(x))],
                ct
            )
            .ConfigureAwait(false);
        var sold = await SoldPetTypesAsync(db, ct).ConfigureAwait(false);

        for (var i = 0; i < types.Count; i++)
        {
            var type = types[i];
            var key = $"pet:{Number(type)}";
            var productName = names.GetValueOrDefault(codes[i]);
            var text = texts.GetValueOrDefault(PET_TYPE_TEXT_PREFIX + Number(type));
            var pageTitle =
                FirstNamed(text, WithoutStarterFood(productName)) ?? $"Pet {Number(type)}";

            plan.Items.Add(
                new PlanItem(
                    new CatalogBuildItem(
                        key,
                        FirstNamed(productName, text) ?? key,
                        codes[i],
                        [new("pet", null, null, Number(type), 1)],
                        pageTitle,
                        null,
                        sold.Contains(type),
                        productName is null ? NO_PRODUCT_NAME : null
                    ),
                    [new CatalogProductDraft(ProductType.Pet, null, Number(type), 1)]
                )
            );
        }

        return null;
    }

    private static string? WithoutStarterFood(string? name) =>
        name is not null && name.EndsWith(STARTER_FOOD_SUFFIX, StringComparison.OrdinalIgnoreCase)
            ? name[..^STARTER_FOOD_SUFFIX.Length]
            : name;

    /// <summary>
    /// A colour family, <c>chair_plasty*1</c> on, in the order of its colours: the colour
    /// grouping window groups offers by the class name before the <c>*</c>.
    /// </summary>
    private async Task<string?> ColoursAsync(
        TurboDbContext db,
        Plan plan,
        string? baseName,
        CancellationToken ct
    )
    {
        var family = baseName?.Trim() ?? string.Empty;

        if (family.Length == 0)
            return "Give the class name the colours share, the part before the *.";

        var start = family + COLOUR_SEPARATOR;
        var definitions = (
            await Furni(db)
                .Where(x => x.Name.StartsWith(start))
                .Select(AsDefinition)
                .ToListAsync(ct)
                .ConfigureAwait(false)
        )
            .Select(x => (Definition: x, Family: Family(x.Name)))
            .Where(x => x.Family.Base == family && x.Family.Colour is not null)
            .OrderBy(x => x.Family.Colour)
            .Select(x => x.Definition)
            .ToList();

        if (definitions.Count == 0)
            return $"No furniture is named {family}*1, {family}*2 and so on.";

        await AddFurniAsync(db, plan, definitions, ct).ConfigureAwait(false);

        return null;
    }

    /// <summary>A furni line, or the furniture whose class names start the same, by class name.</summary>
    private async Task<string?> FurniLineAsync(
        TurboDbContext db,
        Plan plan,
        CatalogBuildRequest request,
        CancellationToken ct
    )
    {
        var line = request.Line?.Trim() ?? string.Empty;
        var prefix = request.Prefix?.Trim() ?? string.Empty;
        var query = Furni(db);

        if (line.Length > 0)
            query = query.Where(x => x.FurniLine == line);
        else if (prefix.Length >= PREFIX_MIN_LENGTH)
            query = query.Where(x => x.Name.StartsWith(prefix));
        else if (prefix.Length > 0)
            return $"A class name prefix is at least {PREFIX_MIN_LENGTH} characters.";
        else
            return "Choose a furni line, or give the start of the class names.";

        var definitions = (await query.Select(AsDefinition).ToListAsync(ct).ConfigureAwait(false))
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToList();

        await AddFurniAsync(db, plan, definitions, ct).ConfigureAwait(false);

        return null;
    }

    /// <summary>
    /// The pet customisation items (shampoos, parts, saddles), whose custom parameters start
    /// with the pet type they are for; that type's only, when one is given.
    /// </summary>
    private async Task<string?> PetCustomizationAsync(
        TurboDbContext db,
        Plan plan,
        int? petType,
        CancellationToken ct
    )
    {
        var definitions = (
            await Furni(db)
                .Where(x =>
                    x.FurniCategory == FurnitureCategory.PetShampoo
                    || x.FurniCategory == FurnitureCategory.PetCustomPart
                    || x.FurniCategory == FurnitureCategory.PetCustomPartShampoo
                    || x.FurniCategory == FurnitureCategory.PetSaddle
                )
                .Select(AsDefinition)
                .ToListAsync(ct)
                .ConfigureAwait(false)
        )
            .Select(x => (Definition: x, Type: PetTypeOf(x.CustomParams)))
            .Where(x => petType is null || x.Type == petType)
            .OrderBy(x => x.Type ?? int.MaxValue)
            .ThenBy(x => x.Definition.Name, StringComparer.Ordinal)
            .Select(x => x.Definition)
            .ToList();

        await AddFurniAsync(db, plan, definitions, ct).ConfigureAwait(false);

        return null;
    }

    /// <summary>The pet type a customisation item is for: the first word of its custom parameters.</summary>
    private static int? PetTypeOf(string? customParams) =>
        int.TryParse(
            (customParams ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var type
        )
            ? type
            : null;

    /// <summary>
    /// Every avatar effect the client has a name for (product data <c>avatar_effect12</c>, text
    /// <c>fx_12</c>) that a player may own, by id.
    /// </summary>
    private async Task<string?> EffectsAsync(TurboDbContext db, Plan plan, CancellationToken ct)
    {
        var products = (
            await db
                .GamedataProducts.AsNoTracking()
                .Where(x => x.Code.StartsWith(EFFECT_PRODUCT_PREFIX))
                .Select(x => new { x.Code, x.Name })
                .ToListAsync(ct)
                .ConfigureAwait(false)
        )
            .Select(x => (Id: NumberAfter(EffectProductPattern(), x.Code), x.Name))
            .Where(x => x.Id is not null)
            .GroupBy(x => x.Id!.Value)
            .ToDictionary(g => g.Key, g => g.First().Name);
        var texts = (
            await db
                .GamedataTexts.AsNoTracking()
                .Where(x => x.Key.StartsWith(EFFECT_TEXT_PREFIX))
                .Select(x => new { x.Key, x.Value })
                .ToListAsync(ct)
                .ConfigureAwait(false)
        )
            .Select(x => (Id: NumberAfter(EffectTextPattern(), x.Key), x.Value))
            .Where(x => x.Id is not null)
            .GroupBy(x => x.Id!.Value)
            .ToDictionary(g => g.Key, g => g.First().Value);
        var sold = await SoldEffectsAsync(db, ct).ConfigureAwait(false);

        foreach (var id in products.Keys.Union(texts.Keys).Where(effects.CanGive).Order())
        {
            var key = $"fx:{Number(id)}";
            var named = products.TryGetValue(id, out var productName);

            plan.Items.Add(
                new PlanItem(
                    new CatalogBuildItem(
                        key,
                        FirstNamed(productName, texts.GetValueOrDefault(id)) ?? key,
                        EFFECT_PRODUCT_PREFIX + Number(id),
                        [new("effect", null, null, Number(id), 1)],
                        null,
                        null,
                        sold.Contains(id),
                        named ? null : NO_PRODUCT_NAME
                    ),
                    [new CatalogProductDraft(ProductType.Effect, null, Number(id), 1)]
                )
            );
        }

        return null;
    }

    /// <summary>
    /// The limited offers on other pages with nothing left of their series, to move here: the
    /// sold limited items window lists what has sold out. A product's series is the one the
    /// catalog sells, the active one, else the newest.
    /// </summary>
    private static async Task<string?> SoldLimitedAsync(
        TurboDbContext db,
        Plan plan,
        int pageId,
        CancellationToken ct
    )
    {
        var series = await db
            .LtdSeries.AsNoTracking()
            .Select(x => new
            {
                x.CatalogProductEntityId,
                x.RemainingQuantity,
                x.IsActive,
                x.StartsAt,
                x.CreatedAt,
                OfferId = x.CatalogProduct!.CatalogOfferEntityId,
                PageId = x.CatalogProduct.Offer.CatalogPageEntityId,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var offerIds = series
            .Where(x => x.PageId != pageId)
            .GroupBy(x => x.CatalogProductEntityId)
            .Select(g =>
                g.OrderByDescending(x => x.IsActive)
                    .ThenByDescending(x => x.StartsAt ?? x.CreatedAt)
                    .First()
            )
            .Where(x => x.RemainingQuantity == 0)
            .Select(x => x.OfferId)
            .Distinct()
            .ToList();
        var offers = await db
            .CatalogOffers.AsNoTracking()
            .Where(x => offerIds.Contains(x.Id))
            .Include(x => x.Products!)
                .ThenInclude(x => x.FurnitureDefinition)
            .OrderBy(x => x.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var offer in offers)
        {
            var products = (offer.Products ?? []).OrderBy(x => x.Id).ToList();
            var key = $"offer:{Number(offer.Id)}";

            plan.Items.Add(
                new PlanItem(
                    new CatalogBuildItem(
                        key,
                        offer.LocalizationId.Length > 0 ? offer.LocalizationId : key,
                        offer.LocalizationId,
                        [
                            .. products.Select(x => new CatalogBuildProduct(
                                AdminCatalogQueries.TypeName(x.ProductType),
                                x.FurnitureDefinitionEntityId,
                                x.FurnitureDefinition?.Name,
                                x.ExtraParam,
                                x.Quantity
                            )),
                        ],
                        null,
                        offer.Id,
                        false,
                        null
                    ),
                    []
                )
            );
        }

        return null;
    }

    /// <summary>
    /// Every room paper pattern the product data names (<c>floor_single_101</c>): floors, then
    /// wallpapers, then landscapes, each by pattern. An offer each of the paper's one definition
    /// with the pattern as its extra parameter, which the bought item carries.
    /// </summary>
    private static async Task<string?> SpacesAsync(
        TurboDbContext db,
        Plan plan,
        CancellationToken ct
    )
    {
        var papers = await DefinitionsNamedAsync(db, [.. RoomPapers.Select(x => x.Name)], ct)
            .ConfigureAwait(false);
        var sold = await SoldWithParamAsync(db, [.. papers.Values.Select(x => x.Id)], ct)
            .ConfigureAwait(false);

        foreach (var (name, codePrefix) in RoomPapers)
        {
            if (!papers.TryGetValue(name, out var definition))
            {
                plan.Warnings.Add(
                    $"There is no {name} item, so there are no {name} patterns; the page previews a room only once it sells floors, wallpapers and landscapes."
                );

                continue;
            }

            var patterns = await NumberedCodesAsync(db, codePrefix, ct).ConfigureAwait(false);

            if (patterns.Count == 0)
                plan.Warnings.Add(
                    $"There are no {name} patterns; the page previews a room only once it sells floors, wallpapers and landscapes."
                );

            foreach (var (code, pattern, productName) in patterns)
            {
                plan.Items.Add(
                    NumberedItem(
                        $"space:{code}",
                        code,
                        $"{FirstNamed(productName, definition.PublicName) ?? name} {pattern}",
                        definition,
                        pattern,
                        sold,
                        name
                    )
                );
            }
        }

        return null;
    }

    /// <summary>
    /// Every poster the product data names (<c>poster 12</c>), by id: an offer each of the one
    /// poster definition with the id as its extra parameter.
    /// </summary>
    private static async Task<string?> PostersAsync(
        TurboDbContext db,
        Plan plan,
        CancellationToken ct
    )
    {
        var posters = await DefinitionsNamedAsync(db, [POSTER_NAME], ct).ConfigureAwait(false);

        if (!posters.TryGetValue(POSTER_NAME, out var definition))
        {
            plan.Warnings.Add($"There is no {POSTER_NAME} item, so there are no posters to sell.");

            return null;
        }

        var sold = await SoldWithParamAsync(db, [definition.Id], ct).ConfigureAwait(false);
        var codes = await NumberedCodesAsync(db, POSTER_CODE_PREFIX, ct).ConfigureAwait(false);

        foreach (var (code, id, productName) in codes)
        {
            plan.Items.Add(
                NumberedItem(
                    $"poster:{id}",
                    code,
                    FirstNamed(productName) ?? code,
                    definition,
                    id,
                    sold,
                    null
                )
            );
        }

        return null;
    }

    /// <summary>
    /// The badge displays: floor items with the badge display logic, else, on a hotel whose
    /// definitions are not mapped to it yet, those whose class name starts with
    /// <c>badge_display</c>. Named <c>a0 &lt;name&gt;</c> where the product data has that code,
    /// else by class name.
    /// </summary>
    private static async Task<string?> BadgeDisplaysAsync(
        TurboDbContext db,
        Plan plan,
        CancellationToken ct
    )
    {
        var floor = db
            .FurnitureDefinitions.AsNoTracking()
            .Where(x => x.ProductType == ProductType.Floor);
        var definitions = await floor
            .Where(x => x.Logic == BadgeDisplayData.LOGIC_NAME)
            .Select(AsDefinition)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (definitions.Count == 0)
            definitions = await floor
                .Where(x => x.Name.StartsWith(BadgeDisplayData.LOGIC_NAME))
                .Select(AsDefinition)
                .ToListAsync(ct)
                .ConfigureAwait(false);

        definitions = [.. definitions.OrderBy(x => x.Name, StringComparer.Ordinal)];

        var codes = definitions.Select(x => PRODUCT_CODE_PREFIX + x.Name).ToList();
        var names = await ProductNamesAsync(db, codes, ct).ConfigureAwait(false);
        var sold = await SoldDefinitionsAsync(db, ct).ConfigureAwait(false);

        for (var i = 0; i < definitions.Count; i++)
        {
            var named = names.TryGetValue(codes[i], out var productName);

            plan.Items.Add(
                FurniItem(
                    $"def:{Number(definitions[i].Id)}",
                    named ? codes[i] : definitions[i].Name,
                    definitions[i],
                    productName,
                    sold,
                    null
                )
            );
        }

        return null;
    }

    /// <summary>
    /// A disk of each official song (every song, with a warning, while none is marked official),
    /// by song name: the song disk item with the song's id as its extra parameter, which the
    /// bought disk carries and the page's preview plays.
    /// <para>
    /// The name key: the song disk page shows the product data name of the offer's name key (else
    /// the key's text), never the song's own name. Habbo's product data names its song disks
    /// <c>SONG &lt;code&gt;</c> after the official song's code, so a song with a code that the
    /// product data has is named so and shows its name; any other is named <c>song_disk</c>, the
    /// item's own name, as an offer of the disk alone would be. The code is never the extra
    /// parameter: the page would preview it, but the purchase puts only an id on the disk.
    /// </para>
    /// </summary>
    private static async Task<string?> SongDiscsAsync(
        TurboDbContext db,
        Plan plan,
        CancellationToken ct
    )
    {
        var disks = await db
            .FurnitureDefinitions.AsNoTracking()
            .Where(x =>
                x.FurniCategory == FurnitureCategory.TraxSong && x.ProductType == ProductType.Floor
            )
            .Select(AsDefinition)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var disk = disks
            .OrderByDescending(x => x.Name == SONG_DISK_NAME)
            .ThenBy(x => x.Id)
            .FirstOrDefault();

        if (disk is null)
        {
            plan.Warnings.Add("There is no song disk item (furni category 8) to put songs on.");

            return null;
        }

        var songs = db.Songs.AsNoTracking();

        if (!await songs.AnyAsync(x => x.IsOfficial, ct).ConfigureAwait(false))
            plan.Warnings.Add("No song is marked official; showing every song.");
        else
            songs = songs.Where(x => x.IsOfficial);

        var list = (
            await songs
                .Select(x => new
                {
                    x.Id,
                    x.Code,
                    x.Name,
                    x.Author,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false)
        ).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Id).ToList();
        var codes = list.Where(x => !string.IsNullOrWhiteSpace(x.Code))
            .Select(x => SONG_PRODUCT_PREFIX + x.Code!.Trim())
            .ToList();
        var names = await ProductNamesAsync(db, codes, ct).ConfigureAwait(false);
        var sold = await SoldWithParamAsync(db, [disk.Id], ct).ConfigureAwait(false);

        foreach (var song in list)
        {
            var code = string.IsNullOrWhiteSpace(song.Code)
                ? null
                : SONG_PRODUCT_PREFIX + song.Code.Trim();
            var named = code is not null && names.ContainsKey(code);

            plan.Items.Add(
                NumberedItem(
                    $"song:{Number(song.Id)}",
                    named ? code! : disk.Name,
                    $"{song.Name} by {song.Author}",
                    disk,
                    Number(song.Id),
                    sold,
                    named ? null : NO_PRODUCT_NAME
                )
            );
        }

        return null;
    }

    /// <summary>The definitions with exactly these class names, by name; the first by id where two share one.</summary>
    private static async Task<Dictionary<string, Definition>> DefinitionsNamedAsync(
        TurboDbContext db,
        List<string> names,
        CancellationToken ct
    ) =>
        (
            await db
                .FurnitureDefinitions.AsNoTracking()
                .Where(x => names.Contains(x.Name))
                .Select(AsDefinition)
                .ToListAsync(ct)
                .ConfigureAwait(false)
        )
            .Where(x => names.Contains(x.Name, StringComparer.Ordinal))
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.MinBy(x => x.Id)!, StringComparer.Ordinal);

    /// <summary>
    /// The product data codes that start with <paramref name="prefix"/>, with what follows it (a
    /// pattern or a poster id) and their names, in the order of that value's numbers.
    /// </summary>
    private static async Task<List<NumberedCode>> NumberedCodesAsync(
        TurboDbContext db,
        string prefix,
        CancellationToken ct
    )
    {
        var rows = await db
            .GamedataProducts.AsNoTracking()
            .Where(x => x.Code.StartsWith(prefix))
            .Select(x => new { x.Code, x.Name })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Codes are binary-collated, and the value goes onto the item as it is: a code that only
        // starts the same in another case, or whose value is blank or spaced, is not one of these.
        return
        [
            .. rows.Where(x =>
                    x.Code.StartsWith(prefix, StringComparison.Ordinal)
                    && x.Code.Length > prefix.Length
                    && !x.Code.AsSpan(prefix.Length).ContainsAny(' ', '\t')
                )
                .Select(x => new NumberedCode(x.Code, x.Code[prefix.Length..], x.Name))
                .Order(NumberedCode.ByValue),
        ];
    }

    /// <summary>The (definition, extra parameter) pairs an offer already sells, of these definitions.</summary>
    private static async Task<HashSet<(int, string)>> SoldWithParamAsync(
        TurboDbContext db,
        List<int> definitionIds,
        CancellationToken ct
    )
    {
        var rows = await db
            .CatalogProducts.AsNoTracking()
            .Where(x =>
                x.FurnitureDefinitionEntityId != null
                && definitionIds.Contains(x.FurnitureDefinitionEntityId.Value)
                && x.ExtraParam != null
            )
            .Select(x => new { Id = x.FurnitureDefinitionEntityId!.Value, x.ExtraParam })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return [.. rows.Select(x => (x.Id, x.ExtraParam!.Trim()))];
    }

    /// <summary>An offer of a definition that is told apart by its extra parameter: a pattern or a poster id.</summary>
    private static PlanItem NumberedItem(
        string key,
        string localizationId,
        string title,
        Definition definition,
        string value,
        HashSet<(int, string)> sold,
        string? note
    ) =>
        new(
            new CatalogBuildItem(
                key,
                title,
                localizationId,
                [
                    new(
                        AdminCatalogQueries.TypeName(definition.Type),
                        definition.Id,
                        definition.Name,
                        value,
                        1
                    ),
                ],
                null,
                null,
                sold.Contains((definition.Id, value)),
                note
            ),
            [new CatalogProductDraft(definition.Type, definition.Id, value, 1)]
        );

    /// <summary>An offer each of floor or wall items, named by class name as Habbo's are.</summary>
    private static async Task AddFurniAsync(
        TurboDbContext db,
        Plan plan,
        List<Definition> definitions,
        CancellationToken ct
    )
    {
        // One definition is every pattern, poster or song; an offer of it without the one it is
        // would be refused at purchase.
        var named = definitions.Where(x => ProductStuffData.IsNamedByProduct(x.Category)).ToList();

        if (named.Any(x => x.Category != FurnitureCategory.TraxSong))
            plan.Warnings.Add(NEEDS_PATTERN);

        if (named.Any(x => x.Category == FurnitureCategory.TraxSong))
            plan.Warnings.Add(NEEDS_SONG);

        definitions = [.. definitions.Except(named)];

        var names = await ProductNamesAsync(db, [.. definitions.Select(x => x.Name)], ct)
            .ConfigureAwait(false);
        var sold = await SoldDefinitionsAsync(db, ct).ConfigureAwait(false);

        foreach (var definition in definitions)
        {
            plan.Items.Add(
                FurniItem(
                    $"def:{Number(definition.Id)}",
                    definition.Name,
                    definition,
                    names.GetValueOrDefault(definition.Name),
                    sold,
                    null
                )
            );
        }
    }

    private static PlanItem FurniItem(
        string key,
        string localizationId,
        Definition definition,
        string? productName,
        HashSet<int> sold,
        string? note
    ) =>
        new(
            new CatalogBuildItem(
                key,
                FirstNamed(productName, definition.PublicName) ?? key,
                localizationId,
                [
                    new(
                        AdminCatalogQueries.TypeName(definition.Type),
                        definition.Id,
                        definition.Name,
                        null,
                        1
                    ),
                ],
                null,
                null,
                sold.Contains(definition.Id),
                note
            ),
            [new CatalogProductDraft(definition.Type, definition.Id, null, 1)]
        );

    /// <summary>A class name's family and colour: <c>chair_plasty*4</c> is (<c>chair_plasty</c>, 4); a name without a numbered colour is its own family.</summary>
    private static (string Base, int? Colour) Family(string name)
    {
        var at = name.LastIndexOf(COLOUR_SEPARATOR);

        if (at < 0)
            return (name, null);

        return int.TryParse(
            name.AsSpan(at + 1),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var colour
        )
            ? (name[..at], colour)
            : (name, null);
    }

    /// <summary>The columns of a furniture definition the builders read.</summary>
    private static readonly Expression<Func<FurnitureDefinitionEntity, Definition>> AsDefinition =
        x => new Definition(
            x.Id,
            x.Name,
            x.ProductType,
            x.PublicName,
            x.CustomParams,
            x.FurniCategory
        );

    /// <summary>The floor and wall items, which an offer of furni can give.</summary>
    private static IQueryable<FurnitureDefinitionEntity> Furni(TurboDbContext db) =>
        db
            .FurnitureDefinitions.AsNoTracking()
            .Where(x => x.ProductType == ProductType.Floor || x.ProductType == ProductType.Wall);

    /// <summary>The product data names of the codes that have one.</summary>
    private static async Task<Dictionary<string, string?>> ProductNamesAsync(
        TurboDbContext db,
        List<string> codes,
        CancellationToken ct
    )
    {
        var distinct = codes.Distinct(StringComparer.Ordinal).ToList();
        var rows = await db
            .GamedataProducts.AsNoTracking()
            .Where(x => distinct.Contains(x.Code))
            .Select(x => new { x.Code, x.Name })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        // Codes are binary-collated: one that differs only in case is another product.
        return rows.Where(x => distinct.Contains(x.Code, StringComparer.Ordinal))
            .GroupBy(x => x.Code, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.Ordinal);
    }

    private static async Task<Dictionary<string, string>> TextsAsync(
        TurboDbContext db,
        List<string> keys,
        CancellationToken ct
    )
    {
        var rows = await db
            .GamedataTexts.AsNoTracking()
            .Where(x => keys.Contains(x.Key))
            .Select(x => new { x.Key, x.Value })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return rows.Where(x => keys.Contains(x.Key, StringComparer.Ordinal))
            .GroupBy(x => x.Key, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Value, StringComparer.Ordinal);
    }

    /// <summary>The floor and wall items an offer already sells on its own.</summary>
    private static async Task<HashSet<int>> SoldDefinitionsAsync(
        TurboDbContext db,
        CancellationToken ct
    ) =>
        [
            .. await db
                .CatalogProducts.AsNoTracking()
                .Where(x =>
                    x.FurnitureDefinitionEntityId != null
                    && (x.ProductType == ProductType.Floor || x.ProductType == ProductType.Wall)
                    && x.Offer.Products!.Count == 1
                )
                .Select(x => x.FurnitureDefinitionEntityId!.Value)
                .Distinct()
                .ToListAsync(ct)
                .ConfigureAwait(false),
        ];

    /// <summary>The pet types an offer already sells, read as the purchase reads them.</summary>
    private static async Task<HashSet<int>> SoldPetTypesAsync(
        TurboDbContext db,
        CancellationToken ct
    )
    {
        var pets = await db
            .CatalogProducts.AsNoTracking()
            .Where(x => x.ProductType == ProductType.Pet)
            .Select(x => new { x.ExtraParam, Name = x.FurnitureDefinition!.Name })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var types = new HashSet<int>();

        foreach (var pet in pets)
        {
            if (PetProductCodes.TryGetTypeId(pet.Name, out var fromName))
                types.Add(fromName);
            else if (
                int.TryParse(
                    pet.ExtraParam?.Trim(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var fromParam
                )
            )
                types.Add(fromParam);
        }

        return types;
    }

    /// <summary>The effects an offer already sells.</summary>
    private static async Task<HashSet<int>> SoldEffectsAsync(
        TurboDbContext db,
        CancellationToken ct
    )
    {
        var effects = await db
            .CatalogProducts.AsNoTracking()
            .Where(x => x.ProductType == ProductType.Effect)
            .Select(x => x.ExtraParam)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return
        [
            .. effects
                .Select(x => EffectProducts.TryGetEffectId(x, out var id) ? id : 0)
                .Where(x => x > 0),
        ];
    }

    private static int? NumberAfter(Regex pattern, string text) =>
        pattern.Match(text) is { Success: true } match
        && int.TryParse(
            match.Groups[1].Value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var number
        )
            ? number
            : null;

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>The first of the names that says something; null when none does.</summary>
    private static string? FirstNamed(params string?[] names) =>
        names.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

    [GeneratedRegex("^avatar_effect([0-9]+)$")]
    private static partial Regex EffectProductPattern();

    [GeneratedRegex("^fx_([0-9]+)$")]
    private static partial Regex EffectTextPattern();

    /// <summary>A furniture definition as the builders read it.</summary>
    private sealed record Definition(
        int Id,
        string Name,
        ProductType Type,
        string? PublicName,
        string? CustomParams,
        FurnitureCategory Category
    );

    /// <summary>A product data code that names a pattern or a poster id, the value after its prefix.</summary>
    private sealed record NumberedCode(string Code, string Value, string? Name)
    {
        /// <summary>
        /// By the value's numbers: <c>1.2</c> before <c>1.10</c> before <c>2</c>, each
        /// dot-separated part compared as a number where it is one.
        /// </summary>
        public static readonly IComparer<NumberedCode> ByValue = Comparer<NumberedCode>.Create(
            (x, y) =>
            {
                var left = x.Value.Split('.');
                var right = y.Value.Split('.');

                for (var i = 0; i < Math.Min(left.Length, right.Length); i++)
                {
                    var order = ComparePart(left[i], right[i]);

                    if (order != 0)
                        return order;
                }

                return left.Length != right.Length
                    ? left.Length.CompareTo(right.Length)
                    : string.CompareOrdinal(x.Value, y.Value);
            }
        );

        /// <summary>Numbers by value and before words; words by their characters.</summary>
        private static int ComparePart(string left, string right)
        {
            var leftIsNumber = long.TryParse(
                left,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var leftNumber
            );
            var rightIsNumber = long.TryParse(
                right,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var rightNumber
            );

            return (leftIsNumber, rightIsNumber) switch
            {
                (true, true) => leftNumber.CompareTo(rightNumber),
                (true, false) => -1,
                (false, true) => 1,
                _ => string.CompareOrdinal(left, right),
            };
        }
    }

    /// <summary>A plan item, and what an offer of it gives as the edit service takes it.</summary>
    private sealed record PlanItem(CatalogBuildItem Item, IReadOnlyList<CatalogProductDraft> Gives);

    /// <summary>A builder's plan as it is worked out: the page it is for, its items and warnings, or why there is none.</summary>
    private sealed class Plan(string builder, string layout, CatalogPageEntity? page)
    {
        public string Builder { get; } = builder;

        public string Layout { get; } = layout;

        public CatalogPageEntity? Page { get; } = page;

        public List<PlanItem> Items { get; } = [];

        public List<string> Warnings { get; } = [];

        public string? Error { get; private init; }

        public static Plan Refused(string error) =>
            new(string.Empty, string.Empty, null) { Error = error };
    }
}
