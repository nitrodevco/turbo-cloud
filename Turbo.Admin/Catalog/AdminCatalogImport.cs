using System;
using System.Collections.Generic;
using System.Globalization;
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
/// Takes Habbo's catalogues in from a capture of what its client was sent: a catalogue's index
/// and its pages, from a JSON capture of the Builders Club or from packet logs of either
/// catalogue. A plan says what that would add and change and changes nothing; applying works the
/// plan out again and makes it through <see cref="ICatalogEditService"/>, as one step of its
/// history, so every check an edit by hand gets applies.
/// <list type="bullet">
/// <item>A page of the capture is a page here by its name (the one the client opens it by), the
/// second of a name the second here: in the Builders Club a page shown only there, in the normal
/// catalogue one shown only in it or hidden. One with none is added, in the capture's order.
/// Habbo's root is not a page to take in: the normal catalogue's tabs are its root's pages, and
/// the Builders Club's pages, under Habbo's one category, go under the tab of that category's
/// name (its furni tab, which the Builders Club cuts down), else the tab the Builders Club opens
/// on now, as the catalogue cuts it.</item>
/// <item>An offer is the same one on its page when it gives the same items (a poster by its
/// number); offers come from the pages, never from the furnidata, which names many offers no page
/// sells. An item is the furniture here of its class name (a JSON capture's) or of its sprite (a
/// packet log's, whose sprites are Habbo's furnidata ids, as this hotel's are).</item>
/// <item>A page hidden in Habbo's normal catalogue is hidden here, so a link still opens it; one
/// hidden in its Builders Club is left out, a page here being shown there or not in it. Left out
/// too: what this hotel has no item for, rented and limited editions (a series is set up here by
/// hand), memberships (sold from the club pages), and in the Builders Club anything but one floor
/// or wall item, and credit furni, which Habbo never lends.</item>
/// <item>A page both logs show is one page here, shown in both catalogues: the normal
/// catalogue's import makes it so (but for a tab, which the Builders Club never shows) and says
/// what it is and sells, the Builders Club having no prices; the Builders Club's import leaves
/// it as it is and puts the pages only it has under it. Given one log, a page shown in both
/// stays so, and a page by the name of one only the other catalogue shows (its import made
/// alone, before) is that page, shown in both now rather than made again. Each import puts only
/// its own catalogue's pages in order, so neither undoes the other's.</item>
/// <item>No page the capture does not have is changed, unless <c>TakeOthersOut</c> takes the
/// pages it lacks out of that catalogue (in the normal catalogue, hides them, but for those that
/// lead to Builders Club pages).</item>
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

    /// <summary>The largest packet log taken in; Habbo's whole normal catalogue logs to some tens of MB.</summary>
    public const long LOG_MAX_BYTES = 256L * 1024 * 1024;

    private const string CAPTURE_LABEL = "imported the Builders Club catalogue from a capture";
    private const string LOGS_LABEL = "imported the catalogue from packet logs";
    private const string NO_CAPTURE_INDEX = "That is not a capture: it has no Builders Club index.";

    /// <summary>How many pages a note names before the rest are counted.</summary>
    private const int LISTED_MAX = 10;

    /// <summary>The product types by the letter the client knows them by.</summary>
    private static readonly Dictionary<string, ProductType> ProductTypes =
        Enum.GetValues<ProductType>().ToDictionary(x => x.ToLegacyString(), StringComparer.Ordinal);

    /// <summary>What the capture would add and change; nothing is changed.</summary>
    public async Task<CatalogBuildOutcome<CatalogImportPlan>> PlanAsync(
        CatalogImportRequest request,
        CancellationToken ct
    )
    {
        if (CaptureSource(request) is not { } source)
            return CatalogBuildOutcome<CatalogImportPlan>.Refused(NO_CAPTURE_INDEX);

        var work = await PlanWorkAsync(source, request.TakeOthersOut, ct).ConfigureAwait(false);

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
        if (CaptureSource(request) is not { } source)
            return CatalogBuildOutcome<CatalogImportPlan>.Refused(NO_CAPTURE_INDEX);

        var (error, plans) = await ApplySourcesAsync(
                editorId,
                CAPTURE_LABEL,
                [source],
                request.TakeOthersOut,
                request.Publish,
                ct
            )
            .ConfigureAwait(false);

        return error is not null
            ? CatalogBuildOutcome<CatalogImportPlan>.Refused(error)
            : CatalogBuildOutcome<CatalogImportPlan>.Done(plans[0]);
    }

    /// <summary>What the packet logs would add and change, each catalogue planned as it is now; nothing is changed.</summary>
    public async Task<CatalogBuildOutcome<CatalogLogImportPlan>> PlanLogsAsync(
        CatalogLogImportRequest request,
        CancellationToken ct
    )
    {
        var (refused, sources) = LogSources(request);

        if (refused is not null)
            return CatalogBuildOutcome<CatalogLogImportPlan>.Refused(refused);

        var plans = new List<CatalogImportPlan>();

        foreach (var source in sources)
        {
            var work = await PlanWorkAsync(source, request.TakeOthersOut, ct).ConfigureAwait(false);

            if (work.Error is { } error)
                return CatalogBuildOutcome<CatalogLogImportPlan>.Refused(error);

            plans.Add(work.Describe(false, false, editor.UnpublishedChanges));
        }

        return CatalogBuildOutcome<CatalogLogImportPlan>.Done(LogPlan(sources, plans));
    }

    /// <summary>
    /// Makes both logs' plans as one step to undo, the normal catalogue's first: the Builders
    /// Club's is worked out once it is made, so its pages go under the tab it opens on then.
    /// </summary>
    public async Task<CatalogBuildOutcome<CatalogLogImportPlan>> ApplyLogsAsync(
        PlayerId editorId,
        CatalogLogImportRequest request,
        CancellationToken ct
    )
    {
        var (refused, sources) = LogSources(request);

        if (refused is not null)
            return CatalogBuildOutcome<CatalogLogImportPlan>.Refused(refused);

        var (error, plans) = await ApplySourcesAsync(
                editorId,
                LOGS_LABEL,
                sources,
                request.TakeOthersOut,
                request.Publish,
                ct
            )
            .ConfigureAwait(false);

        return error is not null
            ? CatalogBuildOutcome<CatalogLogImportPlan>.Refused(error)
            : CatalogBuildOutcome<CatalogLogImportPlan>.Done(LogPlan(sources, plans));
    }

    private static Source? CaptureSource(CatalogImportRequest request)
    {
        if (request.Capture?.BcIndex?.Root is not { } root)
            return null;

        var pages = new Dictionary<int, CatalogCapturePage>();

        foreach (var (_, entry) in request.Capture.BcPages ?? [])
            if (entry.Page is { } page)
                pages[page.PageId] = page;

        return new Source(CatalogType.BuildersClub, root, pages, []);
    }

    /// <summary>Each log given, read for its catalogue: the normal one first. A log with no index of it is refused.</summary>
    private static (string? Error, List<Source> Sources) LogSources(CatalogLogImportRequest request)
    {
        var sources = new List<Source>();

        foreach (
            var (type, text) in new[]
            {
                (CatalogType.Normal, request.NormalLog),
                (CatalogType.BuildersClub, request.BuildersClubLog),
            }
        )
        {
            if (string.IsNullOrWhiteSpace(text))
                continue;

            var capture = CatalogPacketLogReader.Read(PacketLog.Read(text), type);
            var name = type == CatalogType.BuildersClub ? "Builders Club" : "normal";

            if (capture.Root is not { } root)
                return (
                    $"The {name} catalogue's log has no index of it (a CatalogIndex packet): log the client opening that catalogue, then each page (CatalogPage). {string.Join(' ', capture.Notes)}".TrimEnd(),
                    []
                );

            sources.Add(new Source(type, root, capture.Pages, capture.Notes));
        }

        if (sources.Count == 0)
            return ("Give the normal catalogue's log, the Builders Club's, or both.", []);

        // Given both, each knows which of its pages the other shows, to make them one page.
        if (sources is [var normal, var buildersClub])
            sources =
            [
                normal with
                {
                    Shared = ShownNames(buildersClub.Root),
                },
                buildersClub with
                {
                    Shared = ShownNames(normal.Root),
                },
            ];

        return (null, sources);
    }

    private static CatalogLogImportPlan LogPlan(List<Source> sources, List<CatalogImportPlan> plans)
    {
        CatalogImportPlan? Of(CatalogType type) =>
            sources.FindIndex(x => x.Type == type) is >= 0 and var i ? plans[i] : null;

        return new CatalogLogImportPlan(Of(CatalogType.Normal), Of(CatalogType.BuildersClub));
    }

    /// <summary>
    /// Makes each source's plan in turn, as one step: each worked out as the catalogue is once
    /// those before it are made, its new pages and offers saved together, then its changes, then
    /// its rows put in the capture's order. When anything is refused, what was saved is undone.
    /// </summary>
    private async Task<(string? Error, List<CatalogImportPlan> Plans)> ApplySourcesAsync(
        PlayerId editorId,
        string label,
        List<Source> sources,
        bool takeOthersOut,
        bool publish,
        CancellationToken ct
    )
    {
        // Each plan is worked out before anything is made, so one that is refused leaves the
        // catalogue as it was.
        var works = new List<Work>();

        foreach (var source in sources)
        {
            var work = await PlanWorkAsync(source, takeOthersOut, ct).ConfigureAwait(false);

            if (work.Error is { } refused)
                return (refused, []);

            works.Add(work);
        }

        if (!works.Any(x => x.Changes))
            return (
                null,
                [.. works.Select(x => x.Describe(false, false, editor.UnpublishedChanges))]
            );

        var saved = false;
        var error = await editor
            .GroupAsync(
                editorId,
                label,
                async () =>
                {
                    for (var i = 0; i < sources.Count; i++)
                    {
                        // A later catalogue is planned against what the earlier ones made.
                        var work =
                            i == 0
                                ? works[0]
                                : await PlanWorkAsync(sources[i], takeOthersOut, ct)
                                    .ConfigureAwait(false);

                        if (work.Error is { } refused)
                            return refused;

                        works[i] = work;

                        if (work.HasNew)
                        {
                            var built = await editor
                                .BuildTreeAsync(editorId, label, work.Draft(), ct)
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

                        // Made and moved, the rows are put in the capture's order, which only
                        // the saved catalogue can say now that the new ones have ids.
                        var ordered = await PlanWorkAsync(sources[i], takeOthersOut, ct)
                            .ConfigureAwait(false);

                        foreach (var edit in ordered.Order)
                        {
                            var result = await edit(editorId, ct).ConfigureAwait(false);

                            if (!result.Saved)
                                return result.Error ?? "Not saved.";
                        }
                    }

                    return null;
                }
            )
            .ConfigureAwait(false);

        if (error is not null)
        {
            if (saved)
                await editor.UndoAsync(editorId, ct).ConfigureAwait(false);

            return (error, []);
        }

        var published = false;

        if (publish)
        {
            await editor.PublishAsync(editorId, ct).ConfigureAwait(false);
            published = true;
        }

        var plans = works
            .Select(x => x.Describe(true, published, editor.UnpublishedChanges))
            .ToList();

        foreach (var plan in plans)
            logger.LogInformation(
                "Player {PlayerId} {Label}, {Catalog}: {PagesAdded} pages added, {PagesChanged} changed, {PagesOut} taken out, {OffersAdded} offers added, {OffersChanged} changed, {Skipped} parts left out, published {Published}",
                editorId,
                label,
                plan.Catalog,
                plan.Pages.Count(x => x.Action == CatalogImportPageChange.ADD),
                plan.Pages.Count(x => x.Action == CatalogImportPageChange.UPDATE),
                plan.PagesTakenOut.Length,
                plan.OffersAdded,
                plan.OffersChanged,
                plan.Skipped.Length,
                published
            );

        return (null, plans);
    }

    private async Task<Work> PlanWorkAsync(Source source, bool takeOthersOut, CancellationToken ct)
    {
        var builders = source.Type == CatalogType.BuildersClub;
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

        var pagesById = pages.ToDictionary(x => x.Id);
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

        // Habbo's Builders Club is its normal catalogue's furni tab cut down, so its pages go
        // under the tab named as its category is. With none, under the first tab that leads to
        // its pages, as the catalogue cuts it, else the first tab there is. The normal
        // catalogue's tabs are its root's.
        var under = ourRoot;

        if (builders)
        {
            var tabs = Children(ourRoot.Id)
                .Where(x => x.Display != CatalogPageDisplay.Invisible)
                .ToList();
            var categoryName = (source.Root.Children ?? [])
                .FirstOrDefault(x => x.Visible)
                ?.PageName?.Trim();

            if (
                (
                    tabs.FirstOrDefault(x =>
                        categoryName is { Length: > 0 }
                        && string.Equals(x.Name?.Trim(), categoryName, StringComparison.Ordinal)
                    )
                    ?? tabs.FirstOrDefault(LeadsToBuildersClub)
                    ?? tabs.FirstOrDefault()
                )
                is not { } category
            )
                return Work.Refused(
                    "This catalog has no tab to put the Builders Club pages under."
                );

            under = category;
        }

        // The pages this catalogue's import brings in line: the second of a name in the capture
        // is the second here, so a capture that has a name twice takes in the same pages again.
        // The Builders Club's own pages and those it shares with the normal catalogue are kept
        // apart, so a page both show is never matched to a copy only the Builders Club has.
        bool Takes(CatalogPageDisplay display) => display.IsIn(source.Type);

        var ownByName = ByName(x =>
            Takes(x.Display) && !(builders && x.Display == CatalogPageDisplay.Both)
        );
        var sharedByName = builders
            ? ByName(x => x.Display == CatalogPageDisplay.Both)
            : new Dictionary<string, Queue<SavedPage>>(StringComparer.Ordinal);

        // The pages only the other catalogue shows, but for tabs: one the other catalogue's
        // import made alone, before this one, is the same page by its name, to be shown in both.
        var otherByName = ByName(x =>
            x.ParentId != ourRoot.Id
            && x.Display
                == (builders ? CatalogPageDisplay.Regular : CatalogPageDisplay.BuildersClubOnly)
        );

        Dictionary<string, Queue<SavedPage>> ByName(Func<SavedPage, bool> include) =>
            pages
                .Where(x =>
                    x.ParentId is not null && include(x) && !string.IsNullOrWhiteSpace(x.Name)
                )
                .GroupBy(x => x.Name!.Trim(), StringComparer.Ordinal)
                .ToDictionary(
                    g => g.Key,
                    g => new Queue<SavedPage>(g.OrderBy(x => x.Id)),
                    StringComparer.Ordinal
                );

        var definitions = await db
            .FurnitureDefinitions.AsNoTracking()
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.ProductType,
                x.SpriteId,
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var byClass = definitions
            .GroupBy(x => (x.Name, x.ProductType))
            .ToDictionary(g => g.Key, g => g.Min(x => x.Id));
        var bySprite = definitions
            .GroupBy(x => (x.ProductType, x.SpriteId))
            .ToDictionary(g => g.Key, g => g.Min(x => x.Id));
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

        // Every page's, as a page only the other catalogue shows may become this one's too.
        var offers = (
            await db
                .CatalogOffers.AsNoTracking()
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
                    x.Products!.OrderBy(p => p.Id)
                        .Select(p => new SavedProduct(
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

        var work = new Work(source.Type, builders ? under.Localization : string.Empty, editor);
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        var unpriced = new SortedDictionary<int, int>();
        var unopened = new List<string>();
        var matched = new HashSet<int>();
        var matchedNodes = new Dictionary<CatalogCaptureNode, int>(
            ReferenceEqualityComparer.Instance
        );
        var silvered = 0;
        var madeShared = new List<string>();
        var sharedNames = new HashSet<string>(StringComparer.Ordinal);

        // Habbo's Builders Club tree is its root, its one category, then the pages; the normal
        // catalogue's is its root, then the tabs.
        var top = builders
            ? (source.Root.Children ?? [])
                .SelectMany(heading =>
                    (heading.Children ?? []).Select(node =>
                        (Node: node, Above: $"/{heading.PageName}")
                    )
                )
                .ToList()
            : [.. (source.Root.Children ?? []).Select(node => (Node: node, Above: string.Empty))];

        foreach (var (node, above) in top)
            Visit(node, CatalogPageRef.Saved(under.Id), above, false, tab: !builders);

        OrderChildren(under.Id, top.Select(x => x.Node));

        foreach (
            var page in pages.Where(x =>
                x.ParentId is not null
                && !matched.Contains(x.Id)
                && (
                    takeOthersOut
                        && (
                            builders
                                ? x.Display.IsInBuildersClub()
                                // Hiding a tab or heading that leads to Builders Club pages
                                // would take them out of the Builders Club too.
                                : x.Display == CatalogPageDisplay.Regular && !LeadsToBuildersClub(x)
                        )
                    // A copy of a page both show that only the Builders Club has, as an import
                    // made before they were one, is taken out of it: the page is there once.
                    || (
                        x.Display == CatalogPageDisplay.BuildersClubOnly
                        && sharedNames.Contains(x.Name?.Trim() ?? string.Empty)
                    )
                )
            )
        )
            work.TakeOut(page);

        if (madeShared.Count > 0)
            work.Warnings.Add(
                $"{madeShared.Count} pages are in the normal catalogue's log too, and no page here is shown in both catalogues by their names yet: the normal catalogue's import makes them, once, before this one is made, so a plan of both logs lists them in each ({string.Join(", ", madeShared.Take(LISTED_MAX))}{(madeShared.Count > LISTED_MAX ? $" and {madeShared.Count - LISTED_MAX} more" : string.Empty)})."
            );

        foreach (var (type, count) in unpriced)
            work.Warnings.Add(
                $"{count} offers are priced in activity points of type {type}, which this hotel has no currency for: they cost their credits only."
            );

        if (silvered > 0)
            work.Warnings.Add(
                $"{silvered} offers have a silver price as well, which is not taken in: they cost their other prices only."
            );

        if (unopened.Count > 0)
            work.Warnings.Add(
                $"{unopened.Count} pages of the index were never opened in the capture, so they sell nothing: {string.Join(", ", unopened.Take(LISTED_MAX))}{(unopened.Count > LISTED_MAX ? $" and {unopened.Count - LISTED_MAX} more" : string.Empty)}."
            );

        work.Warnings.AddRange(source.Notes);
        work.MissingClasses.AddRange(missing);

        return work;

        void Visit(
            CatalogCaptureNode node,
            CatalogPageRef parent,
            string above,
            bool hiddenAbove,
            bool tab = false
        )
        {
            var name = node.PageName?.Trim() ?? string.Empty;
            var path = $"{above}/{name}";
            source.Pages.TryGetValue(node.PageId, out var captured);
            var capturedOffers = Distinct(captured?.Offers ?? []);
            var hidden = hiddenAbove || !node.Visible;

            if (builders && !node.Visible)
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
                unopened.Add(path);

            // A page the other log shows too is one page here, shown in both catalogues; never a
            // tab, which the Builders Club does not show. So is one by the name of a page here
            // that both show already (in the Builders Club), or that only the other catalogue
            // shows - its import made alone, and given both logs, one the other log shows too.
            var sharing = !tab && !hidden && name.Length > 0;
            var other =
                sharing
                && (source.Shared?.Contains(name) ?? true)
                && otherByName.TryGetValue(name, out var others)
                && others.Count > 0;
            var shared =
                sharing
                && (
                    source.Shared?.Contains(name) == true
                    || other
                    || (sharedByName.TryGetValue(name, out var left) && left.Count > 0)
                );

            var draft = new CatalogPageDraft(
                (node.Localization ?? name).Trim(),
                name.Length == 0 ? null : name,
                node.Icon,
                captured?.Layout is { Length: > 0 } layout ? layout : HEADING_LAYOUT,
                [.. captured?.Localization?.ImageDatas ?? []],
                [.. captured?.Localization?.TextDatas ?? []],
                shared ? CatalogPageDisplay.Both
                    : builders ? CatalogPageDisplay.BuildersClubOnly
                    : hidden ? CatalogPageDisplay.Invisible
                    : CatalogPageDisplay.Regular
            );

            if (editor.CheckNewPage(draft) is { } refusedPage)
            {
                work.Skipped.Add(
                    new CatalogImportSkip(path, refusedPage, capturedOffers.Count + Below(node))
                );

                return;
            }

            // What a page both show is and sells is the normal catalogue's to say, the Builders
            // Club having no prices; its import takes in only the pages under it that it alone has.
            var makeBoth = false;

            if (
                builders
                && shared
                && (
                    (
                        sharedByName.TryGetValue(name, out var both)
                        && both.TryDequeue(out var candidate)
                    ) || (other && (makeBoth = otherByName[name].TryDequeue(out candidate)))
                )
                && candidate is { } sharedPage
            )
            {
                work.Names.Add(name);
                sharedNames.Add(name);
                matched.Add(sharedPage.Id);
                matchedNodes[node] = sharedPage.Id;
                work.Share(sharedPage, path, makeBoth);

                foreach (var child in node.Children ?? [])
                    Visit(child, CatalogPageRef.Saved(sharedPage.Id), path, hidden);

                OrderChildren(sharedPage.Id, node.Children ?? []);

                return;
            }

            if (builders && shared)
            {
                sharedNames.Add(name);

                if (source.Shared?.Contains(name) == true)
                    madeShared.Add(path);
            }

            var wanted = new List<CatalogOfferDraft>();

            foreach (var offer in capturedOffers)
                if (OfferDraft(offer, path) is { } offerDraft)
                    wanted.Add(offerDraft);

            work.Names.Add(name);

            // In the normal catalogue, a page by the name that only the Builders Club shows is
            // the one to show in both, when this catalogue has none of its own by it.
            if (
                name.Length > 0
                && !(builders && shared)
                && (
                    (ownByName.TryGetValue(name, out var same) && same.TryDequeue(out var found))
                    || (!builders && other && otherByName[name].TryDequeue(out found))
                )
                && found is { } existing
            )
            {
                // Without the Builders Club's log, what it shows is not this import's to change.
                if (
                    !builders
                    && source.Shared is null
                    && existing.Display == CatalogPageDisplay.Both
                    && !hidden
                )
                    draft = draft with { Display = CatalogPageDisplay.Both };

                matched.Add(existing.Id);
                matchedNodes[node] = existing.Id;
                var page = work.Keep(existing, draft, path, parent);
                var have = offers[existing.Id].OrderBy(x => x.SortOrder).ThenBy(x => x.Id).ToList();
                var used = new HashSet<int>();
                var order = new List<int>();

                // An offer already as the capture has it first, then the first left that gives
                // the same: a page selling one item several ways pairs them up alike each time.
                var pairs = new SavedOffer?[wanted.Count];

                foreach (var exact in new[] { true, false })
                    for (var i = 0; i < wanted.Count; i++)
                    {
                        if (pairs[i] is not null)
                            continue;

                        pairs[i] = have.FirstOrDefault(x =>
                            !used.Contains(x.Id)
                            && SameItems(x, wanted[i])
                            && (!exact || !Differs(x, wanted[i]))
                        );

                        if (pairs[i] is { } paired)
                            used.Add(paired.Id);
                    }

                for (var i = 0; i < wanted.Count; i++)
                {
                    var offerDraft = wanted[i];
                    var saved = pairs[i];

                    if (saved is null)
                    {
                        work.AddOffer(page, CatalogPageRef.Saved(existing.Id), offerDraft);
                        continue;
                    }

                    order.Add(saved.Id);

                    if (Differs(saved, offerDraft))
                        work.ChangeOffer(page, saved.Id, offerDraft with { PageId = existing.Id });
                    else
                        work.UnchangedOffers++;
                }

                work.KeptOffers += have.Count - used.Count;

                // The capture's offers in its order, after what it does not have: each moved to
                // the end in turn, as a move takes the offer out before putting it back.
                var positions = have.Select((x, i) => (x.Id, i)).ToDictionary(x => x.Id, x => x.i);
                var kept = have.Count - used.Count;

                if (order.Where((id, i) => positions[id] != kept + i).Any())
                    foreach (var offerId in order)
                        work.Order.Add(
                            (who, token) =>
                                editor.MoveOfferAsync(
                                    who,
                                    offerId,
                                    existing.Id,
                                    int.MaxValue,
                                    token
                                )
                        );

                foreach (var child in node.Children ?? [])
                    Visit(child, CatalogPageRef.Saved(existing.Id), path, hidden);

                // The capture's pages under this one, in its order, after those it does not have.
                OrderChildren(existing.Id, node.Children ?? []);
            }
            else
            {
                var made = work.AddPage(parent, draft, path);

                foreach (var offerDraft in wanted)
                    work.AddOffer(made, made.Ref, offerDraft);

                foreach (var child in node.Children ?? [])
                    Visit(child, made.Ref, path, hidden);
            }
        }

        // Each catalogue puts only its own pages in order, and the Builders Club leaves the pages
        // both show to the normal catalogue's: so neither import undoes the other's order.
        bool Orders(SavedPage page) =>
            builders
                ? page.Display == CatalogPageDisplay.BuildersClubOnly
                : page.Display != CatalogPageDisplay.BuildersClubOnly;

        void OrderChildren(int parentId, IEnumerable<CatalogCaptureNode> nodes)
        {
            var mine = nodes
                .Select(x => matchedNodes.GetValueOrDefault(x))
                .Where(x => x != 0 && pagesById[x].ParentId == parentId && Orders(pagesById[x]))
                .ToList();
            var siblings = Children(parentId).Where(Orders).Select(x => x.Id).ToList();
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

            if (builders && offer.Products is not [_])
                return Skip("it gives more or less than one item");

            if (offer.Products is not { Length: > 0 } products)
                return Skip("it gives nothing");

            if (offer.IsRent)
                return Skip("it is rented, and this hotel sells furni outright");

            var gives = new List<CatalogProductDraft>(products.Length);

            foreach (var product in products)
            {
                if (ProductDraft(product, offer, what) is not { } productDraft)
                    return null;

                gives.Add(productDraft);
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

            // A badge, a bot or a pet is given once per purchase, so the editor never lets one be
            // bought in bulk; asking for it would differ from what is saved every time.
            var oncePerPurchase = gives.Any(x =>
                x.Type is ProductType.Badge or ProductType.Robot or ProductType.Pet
            );
            var draft = new CatalogOfferDraft(
                0,
                (
                    offer.LocalizationId is { Length: > 0 } localization
                        ? localization
                        : products[0].ClassName ?? string.Empty
                ).Trim(),
                Math.Max(0, offer.CostCredits),
                costCurrency,
                currencyId,
                offer.CanGift,
                offer.CanBundle && !oncePerPurchase,
                Math.Clamp(offer.ClubLevel, 0, 2),
                true,
                gives.Count == 1 ? gives[0] : null,
                null,
                gives.Count == 1 ? null : gives
            );

            if (editor.CheckNewOffer(draft) is { } refused)
                return Skip(refused);

            if (offer.CostSilver > 0)
                silvered++;

            return draft;

            CatalogOfferDraft? Skip(string reason)
            {
                work.Skipped.Add(new CatalogImportSkip(what, reason, 1));

                return null;
            }
        }

        CatalogProductDraft? ProductDraft(
            CatalogCaptureProduct product,
            CatalogCaptureOffer offer,
            string what
        )
        {
            if (!ProductTypes.TryGetValue(product.ProductType ?? string.Empty, out var type))
                return Skip(
                    $"it gives a kind of product this hotel has none of ({product.ProductType})"
                );

            if (product.LimitedSize > 0)
                return Skip("it is a limited edition; set a limited series up here by hand");

            var quantity = Math.Max(1, product.Quantity);
            var extraParam = product.ExtraParam is { Length: > 0 } extra ? extra : null;

            if (type is ProductType.Floor or ProductType.Wall)
            {
                var className = product.ClassName is { Length: > 0 } named ? named : null;

                if (
                    builders
                    && className?.StartsWith(
                        CREDIT_FURNI_PREFIX,
                        StringComparison.OrdinalIgnoreCase
                    ) == true
                )
                    return Skip("credit furni are never lent");

                if (className is null && product.SpriteId <= 0)
                    return Skip("the capture names no class for it");

                var found = className is not null
                    ? byClass.TryGetValue((className, type), out var byName)
                        ? byName
                        : (int?)null
                    : bySprite.TryGetValue((type, product.SpriteId), out var sprite)
                        ? sprite
                        : null;

                if (found is not { } definitionId)
                {
                    var item =
                        className
                        ?? $"{(type == ProductType.Floor ? "floor" : "wall")} sprite {product.SpriteId}";
                    missing.Add(item);

                    return Skip($"no furniture here is {item}");
                }

                return new CatalogProductDraft(type, definitionId, extraParam, quantity);
            }

            if (builders)
                return Skip("it gives no floor or wall item");

            switch (type)
            {
                // The client knows an effect by the sprite it sends; the editor keeps the id as
                // the extra parameter.
                case ProductType.Effect:
                    return new CatalogProductDraft(
                        type,
                        null,
                        product.SpriteId.ToString(CultureInfo.InvariantCulture),
                        quantity
                    );
                case ProductType.Badge:
                case ProductType.Robot:
                    return new CatalogProductDraft(type, null, extraParam, 1);
                // A pet's type is its extra parameter, or the digits the offer's name ends in
                // (a0 pet3), which the pet page reads too.
                case ProductType.Pet:
                    var petType = extraParam ?? TrailingDigits(offer.LocalizationId);

                    return petType is null
                        ? Skip("it names no pet type")
                        : new CatalogProductDraft(type, null, petType, 1);
                case ProductType.HabboClub:
                    return Skip("it is a membership, which this hotel sells from its club pages");
                default:
                    return Skip(
                        $"it gives a kind of product this hotel has none of ({product.ProductType})"
                    );
            }

            CatalogProductDraft? Skip(string reason)
            {
                work.Skipped.Add(new CatalogImportSkip(what, reason, 1));

                return null;
            }
        }

        int Below(CatalogCaptureNode node) =>
            (node.Children ?? []).Sum(x =>
                (
                    source.Pages.TryGetValue(x.PageId, out var page)
                        ? Distinct(page.Offers ?? []).Count
                        : 0
                ) + Below(x)
            );
    }

    private static string? TrailingDigits(string? text)
    {
        if (text is null)
            return null;

        var start = text.Length;

        while (start > 0 && char.IsAsciiDigit(text[start - 1]))
            start--;

        return start < text.Length ? text[start..] : null;
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

    private static IReadOnlyList<CatalogProductDraft> Gives(CatalogOfferDraft draft) =>
        draft.Products ?? [draft.Product!];

    private static bool SameItems(SavedOffer offer, CatalogOfferDraft draft)
    {
        var gives = Gives(draft);

        return offer.Products.Count == gives.Count
            && offer
                .Products.Zip(gives)
                .All(x =>
                    x.First.Type == x.Second.Type
                    && x.First.DefinitionId == x.Second.DefinitionId
                    && (x.First.ExtraParam ?? string.Empty) == (x.Second.ExtraParam ?? string.Empty)
                );
    }

    private static bool Differs(SavedOffer offer, CatalogOfferDraft draft) =>
        (draft.LocalizationId.Length > 0 && offer.LocalizationId != draft.LocalizationId)
        || offer.CostCredits != draft.CostCredits
        || offer.CostCurrency != draft.CostCurrency
        || (draft.CostCurrency > 0 && offer.CurrencyTypeId != draft.CurrencyTypeId)
        || offer.CanGift != draft.CanGift
        || offer.CanBundle != draft.CanBundle
        || offer.ClubLevel != draft.ClubLevel
        || !offer.Visible
        || !offer
            .Products.Select(x => x.Quantity)
            .SequenceEqual(Gives(draft).Select(x => x.Quantity));

    /// <summary>
    /// A catalogue's capture: its index's root, its pages by id, and notes on reading it.
    /// <see cref="Shared"/> names the pages the other catalogue's log, given with it, shows; null
    /// when it was not given.
    /// </summary>
    private sealed record Source(
        CatalogType Type,
        CatalogCaptureNode Root,
        IReadOnlyDictionary<int, CatalogCapturePage> Pages,
        IReadOnlyList<string> Notes,
        IReadOnlySet<string>? Shared = null
    );

    /// <summary>The names of the pages a capture's index shows, every page above them shown too.</summary>
    private static HashSet<string> ShownNames(CatalogCaptureNode root)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        Add(root.Children ?? []);

        return names;

        void Add(IEnumerable<CatalogCaptureNode> nodes)
        {
            foreach (var node in nodes.Where(x => x.Visible))
            {
                if (node.PageName?.Trim() is { Length: > 0 } name)
                    names.Add(name);

                Add(node.Children ?? []);
            }
        }
    }

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

    private sealed class Work(CatalogType catalog, string category, ICatalogEditService? editor)
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

        public static Work Refused(string error) =>
            new(CatalogType.Normal, string.Empty, null) { Error = error };

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

            if (saved.Display != draft.Display)
                page.Fields.Add(draft.Display.ToName());

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

        /// <summary>
        /// A page both catalogues show, which the Builders Club's import leaves as it is; with
        /// <paramref name="makeBoth"/>, one only the normal catalogue showed, shown in both now
        /// and otherwise left as it is.
        /// </summary>
        public void Share(SavedPage saved, string path, bool makeBoth)
        {
            var page = new PlanPage(
                saved.Name ?? string.Empty,
                saved.Localization,
                path,
                CatalogImportPageChange.UNCHANGED,
                saved.Id
            );

            if (makeBoth)
            {
                var draft = new CatalogPageDraft(
                    saved.Localization,
                    saved.Name,
                    saved.Icon,
                    saved.Layout,
                    [.. saved.ImageData ?? []],
                    [.. saved.TextData ?? []],
                    CatalogPageDisplay.Both
                );
                var id = saved.Id;

                page.Fields.Add(CatalogPageDisplay.Both.ToName());
                Edits.Add((who, ct) => editor!.UpdatePageAsync(who, id, draft, ct));
            }

            _pages.Add(page);
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

        /// <summary>
        /// Out of this catalogue: out of the Builders Club, a page in both stays in the normal
        /// catalogue and one only there is hidden; out of the normal catalogue, a page is hidden.
        /// </summary>
        public void TakeOut(SavedPage page)
        {
            var display =
                catalog == CatalogType.BuildersClub && page.Display == CatalogPageDisplay.Both
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
                catalog.ToLegacyString(),
                category,
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
