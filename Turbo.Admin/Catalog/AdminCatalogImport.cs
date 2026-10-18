using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Catalog;

/// <summary>
/// Takes Habbo's Builders Club catalogue in from a capture of what its client was sent: the index
/// and every page. A plan says what that would add and change and changes nothing; applying works
/// the plan out again and makes it through <see cref="ICatalogEditService"/>, as one step of its
/// history, so every check an edit by hand gets applies.
/// <list type="bullet">
/// <item>A page of the capture is a Builders Club page here by its name (the one the client opens
/// it by); one with none is added, shown only in the Builders Club, in the capture's order.
/// Habbo's root and its one category are not pages to take in: the category here is the tab the
/// Builders Club opens on now, as the catalogue cuts it.</item>
/// <item>An offer is the same one on its page when it gives the same item (a poster by its
/// number); offers come from the pages, never from the furnidata, which names many offers no page
/// sells.</item>
/// <item>A page hidden in Habbo's Builders Club is left out: a page here is either shown there or
/// not in it. So is an item whose class name no furniture here has, an item that is not a floor or
/// wall item, and credit furni, which Habbo never lends.</item>
/// <item>Pages shown in the normal catalogue too are never changed, nor is any page the capture
/// does not have, unless <see cref="CatalogImportRequest.TakeOthersOut"/> takes the Builders Club
/// pages it lacks out of the Builders Club.</item>
/// </list>
/// </summary>
public sealed class AdminCatalogImport(
    IDbContextFactory<TurboDbContext> database,
    ICatalogEditService editor,
    ILogger<AdminCatalogImport> logger
)
{
    /// <summary>What Habbo's credit furni class names start with; they are cashed in, never borrowed.</summary>
    public const string CREDIT_FURNI_PREFIX = "CF_";

    /// <summary>The layout a page the capture has no page for (a heading) gets.</summary>
    public const string HEADING_LAYOUT = "default_3x3";

    private const string FLOOR = "s";
    private const string WALL = "i";
    private const string LABEL = "imported the Builders Club catalogue from a capture";

    /// <summary>What the capture would add and change; nothing is changed.</summary>
    public async Task<CatalogBuildOutcome<CatalogImportPlan>> PlanAsync(
        CatalogImportRequest request,
        CancellationToken ct
    )
    {
        var work = await PlanWorkAsync(request, ct).ConfigureAwait(false);

        return work.Error is { } error
            ? CatalogBuildOutcome<CatalogImportPlan>.Refused(error)
            : CatalogBuildOutcome<CatalogImportPlan>.Done(
                work.Describe(false, false, editor.UnpublishedChanges)
            );
    }

    /// <summary>
    /// Makes the plan: the new pages and offers checked and saved together, then the changes, as
    /// one step to undo; when one is refused, what was saved of it is undone and nothing stays.
    /// Then the pages and offers are put in the capture's order, and the catalogue published when
    /// asked, which reloads it and tells every client online.
    /// </summary>
    public async Task<CatalogBuildOutcome<CatalogImportPlan>> ApplyAsync(
        PlayerId editorId,
        CatalogImportRequest request,
        CancellationToken ct
    )
    {
        var work = await PlanWorkAsync(request, ct).ConfigureAwait(false);

        if (work.Error is { } refused)
            return CatalogBuildOutcome<CatalogImportPlan>.Refused(refused);

        if (!work.Changes)
            return CatalogBuildOutcome<CatalogImportPlan>.Done(
                work.Describe(false, false, editor.UnpublishedChanges)
            );

        var saved = false;
        var error = await editor
            .GroupAsync(
                editorId,
                LABEL,
                async () =>
                {
                    if (work.HasNew)
                    {
                        var built = await editor
                            .BuildTreeAsync(editorId, LABEL, work.Draft(), ct)
                            .ConfigureAwait(false);

                        if (!built.Saved)
                            return built.Error;

                        saved = true;
                    }

                    foreach (var edit in work.Edits)
                    {
                        var result = await edit(editorId, ct).ConfigureAwait(false);

                        if (!result.Saved)
                            return result.Error ?? "Not saved.";

                        saved = true;
                    }

                    // Made and moved, the rows are put in the capture's order, which only the
                    // saved catalogue can say now that the new ones have ids.
                    var ordered = await PlanWorkAsync(request, ct).ConfigureAwait(false);

                    foreach (var edit in ordered.Order)
                    {
                        var result = await edit(editorId, ct).ConfigureAwait(false);

                        if (!result.Saved)
                            return result.Error ?? "Not saved.";
                    }

                    return null;
                }
            )
            .ConfigureAwait(false);

        if (error is not null)
        {
            if (saved)
                await editor.UndoAsync(editorId, ct).ConfigureAwait(false);

            return CatalogBuildOutcome<CatalogImportPlan>.Refused(error);
        }

        var published = false;

        if (request.Publish)
        {
            await editor.PublishAsync(editorId, ct).ConfigureAwait(false);
            published = true;
        }

        var plan = work.Describe(true, published, editor.UnpublishedChanges);

        logger.LogInformation(
            "Player {PlayerId} imported the Builders Club catalogue from a capture: {PagesAdded} pages added, {PagesChanged} changed, {PagesOut} taken out, {OffersAdded} offers added, {OffersChanged} changed, {Skipped} parts left out, published {Published}",
            editorId,
            plan.Pages.Count(x => x.Action == CatalogImportPageChange.ADD),
            plan.Pages.Count(x => x.Action == CatalogImportPageChange.UPDATE),
            plan.PagesTakenOut.Length,
            plan.OffersAdded,
            plan.OffersChanged,
            plan.Skipped.Length,
            published
        );

        return CatalogBuildOutcome<CatalogImportPlan>.Done(plan);
    }

    private async Task<Work> PlanWorkAsync(CatalogImportRequest request, CancellationToken ct)
    {
        if (request.Capture?.BcIndex?.Root is not { } root)
            return Work.Refused("That is not a capture: it has no Builders Club index.");

        var capturedPages = new Dictionary<int, CatalogCapturePage>();

        foreach (var (_, entry) in request.Capture.BcPages ?? [])
            if (entry.Page is { } page)
                capturedPages[page.PageId] = page;

        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var pages = await db
            .CatalogPages.AsNoTracking()
            .Select(x => new SavedPage(
                x.Id,
                x.ParentEntityId,
                x.Name,
                x.Localization,
                x.Icon,
                x.Layout,
                x.ImageData,
                x.TextData,
                x.Display,
                x.SortOrder
            ))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (pages.Where(x => x.ParentId is null).MinBy(x => x.Id) is not { } ourRoot)
            return Work.Refused("This catalog has no root page.");

        var byParent = pages.Where(x => x.ParentId is not null).ToLookup(x => x.ParentId!.Value);

        List<SavedPage> Children(int id) =>
            [
                .. byParent[id]
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.Localization)
                    .ThenBy(x => x.Id),
            ];

        bool LeadsToBuildersClub(SavedPage page) =>
            page.Display.IsInBuildersClub() || Children(page.Id).Any(LeadsToBuildersClub);

        // The Builders Club opens on the first tab that leads to its pages, as the catalogue cuts
        // it; with none yet, the first tab there is.
        var tabs = Children(ourRoot.Id)
            .Where(x => x.Display != CatalogPageDisplay.Invisible)
            .ToList();
        var category = tabs.FirstOrDefault(LeadsToBuildersClub) ?? tabs.FirstOrDefault();

        if (category is null)
            return Work.Refused("This catalog has no tab to put the Builders Club pages under.");

        var bcOnlyByName = pages
            .Where(x =>
                x.Display == CatalogPageDisplay.BuildersClubOnly
                && !string.IsNullOrWhiteSpace(x.Name)
            )
            .GroupBy(x => x.Name!.Trim(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.MinBy(x => x.Id)!, StringComparer.Ordinal);
        var sharedNames = pages
            .Where(x => x.Display == CatalogPageDisplay.Both && !string.IsNullOrWhiteSpace(x.Name))
            .Select(x => x.Name!.Trim())
            .ToHashSet(StringComparer.Ordinal);

        var classNames = capturedPages
            .Values.SelectMany(x => x.Offers ?? [])
            .SelectMany(x => x.Products ?? [])
            .Select(x => x.ClassName)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var definitions = (
            await db
                .FurnitureDefinitions.AsNoTracking()
                .Where(x => classNames.Contains(x.Name))
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.ProductType,
                })
                .ToListAsync(ct)
                .ConfigureAwait(false)
        ).Where(x => classNames.Contains(x.Name, StringComparer.Ordinal)).GroupBy(x => (x.Name, x.ProductType)).ToDictionary(g => g.Key, g => g.Min(x => x.Id));
        var currencies = (
            await db
                .CurrencyTypes.AsNoTracking()
                .Where(x => x.Enabled && x.ActivityPointType != null)
                .Select(x => new { x.Id, x.ActivityPointType })
                .ToListAsync(ct)
                .ConfigureAwait(false)
        )
            .GroupBy(x => x.ActivityPointType!.Value)
            .ToDictionary(g => g.Key, g => g.Min(x => x.Id));

        var bcPageIds = pages
            .Where(x => x.Display == CatalogPageDisplay.BuildersClubOnly)
            .Select(x => x.Id)
            .ToList();
        var offers = (
            await db
                .CatalogOffers.AsNoTracking()
                .Where(x => bcPageIds.Contains(x.CatalogPageEntityId))
                .Select(x => new SavedOffer(
                    x.Id,
                    x.CatalogPageEntityId,
                    x.LocalizationId,
                    x.CostCredits,
                    x.CostCurrency,
                    x.CurrencyTypeId,
                    x.CanGift,
                    x.CanBundle,
                    x.ClubLevel,
                    x.Visible,
                    x.SortOrder,
                    x.Products!.Select(p => new SavedProduct(
                            p.ProductType,
                            p.FurnitureDefinitionEntityId,
                            p.ExtraParam,
                            p.Quantity
                        ))
                        .ToList()
                ))
                .ToListAsync(ct)
                .ConfigureAwait(false)
        ).ToLookup(x => x.PageId);

        var work = new Work(category, editor);
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        var unpriced = new SortedDictionary<int, int>();
        var matched = new HashSet<int>();

        foreach (var heading in root.Children ?? [])
        foreach (var node in heading.Children ?? [])
            Visit(node, CatalogPageRef.Saved(category.Id), $"/{heading.PageName}");

        if (request.TakeOthersOut)
            foreach (
                var page in pages.Where(x =>
                    x.Display.IsInBuildersClub() && !matched.Contains(x.Id)
                )
            )
                work.TakeOut(page);

        foreach (var name in sharedNames.Where(x => work.Names.Contains(x)))
            work.Warnings.Add(
                $"{name}: a page shown in both catalogues has this name, so it is left as it is and a Builders Club page is made beside it."
            );

        foreach (var (type, count) in unpriced)
            work.Warnings.Add(
                $"{count} offers are priced in activity points of type {type}, which this hotel has no currency for: they cost their credits only."
            );

        work.MissingClasses.AddRange(missing);

        return work;

        void Visit(CatalogCaptureNode node, CatalogPageRef parent, string above)
        {
            var name = node.PageName?.Trim() ?? string.Empty;
            var path = $"{above}/{name}";
            capturedPages.TryGetValue(node.PageId, out var captured);
            var capturedOffers = Distinct(captured?.Offers ?? []);

            if (!node.Visible)
            {
                work.Skipped.Add(
                    new CatalogImportSkip(
                        path,
                        "hidden in Habbo's Builders Club, and a page here is shown there or not in it",
                        capturedOffers.Count + Below(node)
                    )
                );

                return;
            }

            if (node.PageId > 0 && captured is null)
                work.Warnings.Add($"{path}: the capture has no page for it, so it sells nothing.");

            var draft = new CatalogPageDraft(
                (node.Localization ?? name).Trim(),
                name.Length == 0 ? null : name,
                node.Icon,
                captured?.Layout is { Length: > 0 } layout ? layout : HEADING_LAYOUT,
                [.. captured?.Localization?.ImageDatas ?? []],
                [.. captured?.Localization?.TextDatas ?? []],
                CatalogPageDisplay.BuildersClubOnly
            );
            var wanted = new List<(CatalogCaptureOffer Offer, CatalogOfferDraft Draft)>();

            foreach (var offer in capturedOffers)
                if (OfferDraft(offer, path) is { } offerDraft)
                    wanted.Add((offer, offerDraft));

            work.Names.Add(name);

            if (name.Length > 0 && bcOnlyByName.TryGetValue(name, out var existing))
            {
                matched.Add(existing.Id);
                var page = work.Keep(existing, draft, path, parent);
                var have = offers[existing.Id].OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList();
                var used = new HashSet<int>();
                var order = new List<int>();

                foreach (var (_, offerDraft) in wanted)
                {
                    var same = have.FirstOrDefault(x =>
                        !used.Contains(x.Id) && SameItem(x, offerDraft.Product!)
                    );

                    if (same is null)
                    {
                        work.AddOffer(page, CatalogPageRef.Saved(existing.Id), offerDraft);
                        continue;
                    }

                    used.Add(same.Id);
                    order.Add(same.Id);

                    if (Differs(same, offerDraft))
                        work.ChangeOffer(page, same.Id, offerDraft with { PageId = existing.Id });
                    else
                        work.UnchangedOffers++;
                }

                work.KeptOffers += have.Count - used.Count;

                // The capture's offers in its order, after what it does not have.
                var positions = have.Select((x, i) => (x.Id, i)).ToDictionary(x => x.Id, x => x.i);
                var kept = have.Count - used.Count;

                if (order.Where((id, i) => positions[id] != kept + i).Any())
                    for (var i = 0; i < order.Count; i++)
                    {
                        var offerId = order[i];
                        var index = kept + i;
                        work.Order.Add(
                            (who, token) =>
                                editor.MoveOfferAsync(who, offerId, existing.Id, index, token)
                        );
                    }

                foreach (var child in node.Children ?? [])
                    Visit(child, CatalogPageRef.Saved(existing.Id), path);

                // The capture's pages under this one, in its order, after those it does not have.
                OrderChildren(existing.Id, node);
            }
            else
            {
                var made = work.AddPage(parent, draft, path);

                foreach (var (_, offerDraft) in wanted)
                    work.AddOffer(made, made.Ref, offerDraft);

                foreach (var child in node.Children ?? [])
                    Visit(child, made.Ref, path);
            }
        }

        void OrderChildren(int parentId, CatalogCaptureNode node)
        {
            var mine = (node.Children ?? [])
                .Where(x => x.Visible)
                .Select(x => x.PageName?.Trim() ?? string.Empty)
                .Select(x => bcOnlyByName.GetValueOrDefault(x))
                .OfType<SavedPage>()
                .Where(x => x.ParentId == parentId)
                .Select(x => x.Id)
                .ToList();
            var siblings = Children(parentId).Select(x => x.Id).ToList();
            var others = siblings.Count(x => !mine.Contains(x));

            if (!mine.Where((id, i) => siblings.IndexOf(id) != others + i).Any())
                return;

            foreach (var pageId in mine)
                work.Order.Add(
                    (who, token) => editor.MovePageAsync(who, pageId, parentId, int.MaxValue, token)
                );
        }

        CatalogOfferDraft? OfferDraft(CatalogCaptureOffer offer, string path)
        {
            var what = $"{path}: offer {offer.Id} {offer.LocalizationId}";

            if (offer.Products is not [var product])
            {
                work.Skipped.Add(
                    new CatalogImportSkip(what, "it gives more or less than one item", 1)
                );
                return null;
            }

            var type = product.ProductType switch
            {
                FLOOR => ProductType.Floor,
                WALL => ProductType.Wall,
                _ => (ProductType?)null,
            };

            if (type is null)
            {
                work.Skipped.Add(new CatalogImportSkip(what, "it gives no floor or wall item", 1));
                return null;
            }

            if (product.ClassName is not { Length: > 0 } className)
            {
                work.Skipped.Add(
                    new CatalogImportSkip(what, "the capture names no class for it", 1)
                );
                return null;
            }

            if (className.StartsWith(CREDIT_FURNI_PREFIX, StringComparison.OrdinalIgnoreCase))
            {
                work.Skipped.Add(new CatalogImportSkip(what, "credit furni are never lent", 1));
                return null;
            }

            if (!definitions.TryGetValue((className, type.Value), out var definitionId))
            {
                missing.Add(className);
                work.Skipped.Add(
                    new CatalogImportSkip(what, $"no furniture here is {className}", 1)
                );
                return null;
            }

            var costCurrency = Math.Max(0, offer.CostCurrency);
            int? currencyId = null;

            if (costCurrency > 0)
            {
                if (currencies.TryGetValue(offer.CostCurrencyType, out var id))
                    currencyId = id;
                else
                {
                    unpriced[offer.CostCurrencyType] =
                        unpriced.GetValueOrDefault(offer.CostCurrencyType) + 1;
                    costCurrency = 0;
                }
            }

            return new CatalogOfferDraft(
                0,
                (offer.LocalizationId ?? className).Trim(),
                Math.Max(0, offer.CostCredits),
                costCurrency,
                currencyId,
                offer.CanGift,
                offer.CanBundle,
                Math.Clamp(offer.ClubLevel, 0, 2),
                true,
                new CatalogProductDraft(
                    type.Value,
                    definitionId,
                    product.ExtraParam is { Length: > 0 } extra ? extra : null,
                    Math.Max(1, product.Quantity)
                )
            );
        }

        int Below(CatalogCaptureNode node) =>
            (node.Children ?? []).Sum(x =>
                (
                    capturedPages.TryGetValue(x.PageId, out var page)
                        ? Distinct(page.Offers ?? []).Count
                        : 0
                ) + Below(x)
            );
    }

    /// <summary>
    /// A page's offers once each: a capture can list a page's offers twice over (two replies to
    /// the client's one open read as one), and an offer is on its page once.
    /// </summary>
    private static List<CatalogCaptureOffer> Distinct(CatalogCaptureOffer[] offers)
    {
        var seen = new HashSet<int>();

        return [.. offers.Where(x => seen.Add(x.Id))];
    }

    private static bool SameItem(SavedOffer offer, CatalogProductDraft product) =>
        offer.Products is [var saved]
        && saved.Type == product.Type
        && saved.DefinitionId == product.DefinitionId
        && (saved.ExtraParam ?? string.Empty) == (product.ExtraParam ?? string.Empty);

    private static bool Differs(SavedOffer offer, CatalogOfferDraft draft) =>
        offer.LocalizationId != draft.LocalizationId
        || offer.CostCredits != draft.CostCredits
        || offer.CostCurrency != draft.CostCurrency
        || (draft.CostCurrency > 0 && offer.CurrencyTypeId != draft.CurrencyTypeId)
        || offer.CanGift != draft.CanGift
        || offer.CanBundle != draft.CanBundle
        || offer.ClubLevel != draft.ClubLevel
        || !offer.Visible
        || offer.Products[0].Quantity != draft.Product!.Quantity;

    private sealed record SavedPage(
        int Id,
        int? ParentId,
        string? Name,
        string Localization,
        int Icon,
        string Layout,
        List<string>? ImageData,
        List<string>? TextData,
        CatalogPageDisplay Display,
        int SortOrder
    );

    private sealed record SavedOffer(
        int Id,
        int PageId,
        string LocalizationId,
        int CostCredits,
        int CostCurrency,
        int? CurrencyTypeId,
        bool CanGift,
        bool CanBundle,
        int ClubLevel,
        bool Visible,
        int SortOrder,
        List<SavedProduct> Products
    );

    private sealed record SavedProduct(
        ProductType Type,
        int? DefinitionId,
        string? ExtraParam,
        int Quantity
    );

    private delegate Task<CatalogEditResult> Edit(PlayerId editor, CancellationToken ct);

    /// <summary>A page of the plan, with what happens to it and its offers.</summary>
    private sealed class PlanPage(
        string name,
        string title,
        string path,
        string action,
        int? pageId
    )
    {
        public CatalogPageRef Ref { get; init; } = CatalogPageRef.Saved(pageId ?? 0);

        public List<string> Fields { get; } = [];

        public int OffersAdded { get; set; }

        public int OffersChanged { get; set; }

        public CatalogImportPageChange Describe() =>
            new(
                name,
                title,
                path,
                Fields.Count > 0 && action == CatalogImportPageChange.UNCHANGED
                        ? CatalogImportPageChange.UPDATE
                    : OffersAdded + OffersChanged > 0 && action == CatalogImportPageChange.UNCHANGED
                        ? CatalogImportPageChange.UPDATE
                    : action,
                pageId,
                [.. Fields],
                OffersAdded,
                OffersChanged
            );
    }

    private sealed class Work(SavedPage? category, ICatalogEditService? editor)
    {
        private readonly List<CatalogNewPage> _newPages = [];
        private readonly List<CatalogNewOffer> _newOffers = [];
        private readonly List<CatalogPagePlacement> _pageMoves = [];
        private readonly List<PlanPage> _pages = [];
        private readonly List<PlanPage> _out = [];
        private int _offersChanged;

        public string? Error { get; private init; }

        public List<Edit> Edits { get; } = [];

        public List<Edit> Order { get; } = [];

        public List<CatalogImportSkip> Skipped { get; } = [];

        public List<string> MissingClasses { get; } = [];

        public List<string> Warnings { get; } = [];

        public HashSet<string> Names { get; } = new(StringComparer.Ordinal);

        public int KeptOffers { get; set; }

        public int UnchangedOffers { get; set; }

        public bool HasNew => _newPages.Count > 0 || _newOffers.Count > 0 || _pageMoves.Count > 0;

        public bool Changes => HasNew || Edits.Count > 0 || Order.Count > 0;

        public static Work Refused(string error) => new(null, null) { Error = error };

        public CatalogTreeDraft Draft() => new(_newPages, _newOffers, [], _pageMoves);

        public PlanPage AddPage(CatalogPageRef parent, CatalogPageDraft draft, string path)
        {
            var page = new PlanPage(
                draft.Name ?? string.Empty,
                draft.Localization,
                path,
                CatalogImportPageChange.ADD,
                null
            )
            {
                Ref = CatalogPageRef.Made(_newPages.Count),
            };

            _newPages.Add(new CatalogNewPage(parent, draft));
            _pages.Add(page);

            return page;
        }

        public PlanPage Keep(
            SavedPage saved,
            CatalogPageDraft draft,
            string path,
            CatalogPageRef parent
        )
        {
            var page = new PlanPage(
                draft.Name ?? string.Empty,
                draft.Localization,
                path,
                CatalogImportPageChange.UNCHANGED,
                saved.Id
            );

            if (saved.Localization != draft.Localization)
                page.Fields.Add("title");

            if (saved.Icon != draft.Icon)
                page.Fields.Add("icon");

            if (saved.Layout != draft.Layout)
                page.Fields.Add("layout");

            if (!(saved.ImageData ?? []).SequenceEqual(draft.ImageData))
                page.Fields.Add("images");

            if (!(saved.TextData ?? []).SequenceEqual(draft.TextData))
                page.Fields.Add("texts");

            if (page.Fields.Count > 0)
            {
                var id = saved.Id;
                Edits.Add((who, ct) => editor!.UpdatePageAsync(who, id, draft, ct));
            }

            // Somewhere else in the tree, the page is moved, with what is under it, to the end of
            // the page it is under in the capture: one saved already, or one the plan makes.
            if (parent.Id is { } wantedParent)
            {
                if (saved.ParentId != wantedParent)
                {
                    page.Fields.Add("place");
                    var id = saved.Id;
                    Edits.Add(
                        (who, ct) => editor!.MovePageAsync(who, id, wantedParent, int.MaxValue, ct)
                    );
                }
            }
            else
            {
                page.Fields.Add("place");
                _pageMoves.Add(new CatalogPagePlacement(saved.Id, parent));
            }

            _pages.Add(page);

            return page;
        }

        public void AddOffer(PlanPage page, CatalogPageRef target, CatalogOfferDraft draft)
        {
            _newOffers.Add(new CatalogNewOffer(target, draft));
            page.OffersAdded++;
        }

        public void ChangeOffer(PlanPage page, int offerId, CatalogOfferDraft draft)
        {
            Edits.Add((who, ct) => editor!.UpdateOfferAsync(who, offerId, draft, ct));
            page.OffersChanged++;
            _offersChanged++;
        }

        public void TakeOut(SavedPage page)
        {
            var display =
                page.Display == CatalogPageDisplay.Both
                    ? CatalogPageDisplay.Regular
                    : CatalogPageDisplay.Invisible;
            var draft = new CatalogPageDraft(
                page.Localization,
                page.Name,
                page.Icon,
                page.Layout,
                [.. page.ImageData ?? []],
                [.. page.TextData ?? []],
                display
            );
            var id = page.Id;

            Edits.Add((who, ct) => editor!.UpdatePageAsync(who, id, draft, ct));
            _out.Add(
                new PlanPage(
                    page.Name ?? string.Empty,
                    page.Localization,
                    string.Empty,
                    CatalogImportPageChange.OUT,
                    page.Id
                )
                {
                    Fields = { display.ToName() },
                }
            );
        }

        public CatalogImportPlan Describe(bool saved, bool published, int unpublished) =>
            new(
                category?.Localization ?? string.Empty,
                [.. _pages.Select(x => x.Describe())],
                [.. _out.Select(x => x.Describe())],
                _newOffers.Count,
                _offersChanged,
                KeptOffers,
                UnchangedOffers,
                [.. Skipped],
                [.. MissingClasses],
                [.. Warnings],
                saved,
                published,
                unpublished
            );
    }
}
