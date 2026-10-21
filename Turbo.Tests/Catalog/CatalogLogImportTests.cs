using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Catalog;
using Turbo.Catalog.Editing;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Taking Habbo's catalogues in from packet logs of its client. "Habbo" here is a second hotel,
/// whose index and pages are sent by the real serializers and logged as G-Earth logs them, its
/// client opening the catalogue and then each page. Taken in, the normal catalogue's new page
/// comes with its bundle, badge and effect, and a page hidden there comes hidden; furni this
/// hotel has none of is left out by its sprite; once in, the same log has nothing left to do.
/// Both catalogues' logs are taken in as one step, and a log of the wrong catalogue is refused.
/// </summary>
public sealed class CatalogLogImportTests : IDisposable
{
    private static readonly PlayerId Editor = 1;

    private const int ONLY_AT_HABBO = 40;
    private const int NOVELTIES = 50;
    private const int VAULT = 51;
    private const int BUNDLE = 200;
    private const int BADGE = 201;
    private const int EFFECT = 202;
    private const int UNKNOWN_FURNI = 203;
    private const int IN_VAULT = 204;
    private const int BC_CHAIR = 205;

    private readonly CatalogFixture _habbo = new();
    private readonly CatalogFixture _hotel = new();
    private readonly CatalogEditService _service;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CatalogLogImportTests()
    {
        _service = new CatalogEditService(
            _hotel.Db,
            _hotel.Definitions,
            _hotel.NormalProvider(),
            _hotel.BuildersClubProvider(),
            _hotel.Fakes.Create<ISessionGateway>(),
            _hotel.Fakes.Create<IGrainFactory>(),
            new CapturingLogger<ICatalogEditService>()
        );

        _habbo.AddDefinition(ONLY_AT_HABBO, "habbo_only");
        _habbo.Db.Insert(Page(NOVELTIES, FURNITURE, "Novelties", sort: 5));
        _habbo.Db.Insert(Page(VAULT, ROOT, "Vault", CatalogPageDisplay.Invisible, sort: 5));
        HabboOffer(
            BUNDLE,
            NOVELTIES,
            "bundle",
            (ProductType.Floor, CHAIR, null),
            (ProductType.Wall, POSTER, "7")
        );
        HabboOffer(BADGE, NOVELTIES, "badge", (ProductType.Badge, null, "ACH_Test1"));
        HabboOffer(EFFECT, NOVELTIES, "effect", (ProductType.Effect, null, "5"));
        HabboOffer(
            UNKNOWN_FURNI,
            NOVELTIES,
            "habbo_only",
            (ProductType.Floor, ONLY_AT_HABBO, null)
        );
        HabboOffer(IN_VAULT, VAULT, "vaulted", (ProductType.Floor, CHAIR, null));
        HabboOffer(BC_CHAIR, BUILDERS_PAGE, "bc_chair", (ProductType.Floor, CHAIR, null));
    }

    public void Dispose()
    {
        _habbo.Dispose();
        _hotel.Dispose();
    }

    private AdminCatalogImport Importer =>
        new(_hotel.Db, _service, new CapturingLogger<AdminCatalogImport>());

    private void HabboOffer(
        int id,
        int pageId,
        string name,
        params (ProductType Type, int? Definition, string? Extra)[] products
    )
    {
        _habbo.Db.Insert(
            new CatalogOfferEntity
            {
                Id = id,
                CatalogPageEntityId = pageId,
                LocalizationId = name,
                CostCredits = 3,
                CostCurrency = 0,
                ClubLevel = 0,
                CanGift = true,
                CanBundle = true,
                Visible = true,
                SortOrder = id,
                Page = null!,
            }
        );

        for (var i = 0; i < products.Length; i++)
            _habbo.Db.Insert(
                new CatalogProductEntity
                {
                    Id = id * 10 + i,
                    CatalogOfferEntityId = id,
                    ProductType = products[i].Type,
                    FurnitureDefinitionEntityId = products[i].Definition,
                    ExtraParam = products[i].Extra,
                    Quantity = 1,
                    Offer = null!,
                }
            );
    }

    /// <summary>Habbo's client opening the catalogue, then each page of it, as G-Earth logs it.</summary>
    private async Task<string> HabboLogAsync(CatalogType type)
    {
        CatalogSnapshot snapshot;

        if (type == CatalogType.BuildersClub)
        {
            var provider = _habbo.BuildersClubProvider();
            await provider.ReloadAsync(Ct);
            snapshot = provider.Current;
        }
        else
        {
            var provider = _habbo.NormalProvider();
            await provider.ReloadAsync(Ct);
            snapshot = provider.Current;
        }

        var log = new StringBuilder();
        Logged(log, "CatalogIndex", new CatalogIndexMessageComposer { Catalog = snapshot });

        var harness = new PacketHarness();
        harness.Fakes.Handlers["GetCatalogSnapshot"] = _ => snapshot;
        harness.Resolver.Overrides[typeof(ICatalogService)] =
            harness.Fakes.Create<ICatalogService>();

        foreach (var pageId in snapshot.PagesById.Keys.Where(x => x != snapshot.RootPageId).Order())
        {
            await harness.SendAsync(
                PacketHarness.Incoming("GetCatalogPageMessageEvent"),
                PacketHarness.Payload(w => w.Int(pageId).Int(-1).String(type.ToLegacyString()))
            );

            while (harness.Sent.TryDequeue(out var sent))
                if (sent is CatalogPageMessageComposer)
                    Logged(log, "CatalogPage", sent);
        }

        return log.ToString();
    }

    /// <summary>A packet as G-Earth logs it: its name, then its bytes, a byte below 32, 127 and the brackets and braces as <c>[n]</c>.</summary>
    private static void Logged(StringBuilder log, string name, IComposer composer)
    {
        var serializer = PacketHarness.Revision.Serializers[composer.GetType()];
        var packet = serializer.Serialize(composer);
        byte[] framed;

        try
        {
            framed = packet.ToArray();
        }
        finally
        {
            (packet as IDisposable)?.Dispose();
        }

        log.Append('[').Append(name).AppendLine("]");
        log.Append("Incoming[").Append(serializer.Header).Append("] -> ");

        foreach (var b in framed)
        {
            if (b < 32 || b is (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or 127)
                log.Append('[').Append(b).Append(']');
            else
                log.Append((char)b);
        }

        log.AppendLine();
    }

    private static string Gives(CatalogOfferEntity offer) =>
        string.Join(
            ',',
            offer
                .Products!.OrderBy(p => p.Id)
                .Select(p => $"{p.ProductType}:{p.FurnitureDefinitionEntityId}:{p.ExtraParam}")
        );

    [Fact]
    public async Task ThePlan_AddsWhatThisHotelLacks_LeavesOutFurniItHasNoneOf_AndChangesNothing()
    {
        var log = await HabboLogAsync(CatalogType.Normal);
        var before = _hotel.Rows();

        var outcome = await Importer.PlanLogsAsync(
            new CatalogLogImportRequest(log, null, false, false),
            Ct
        );

        outcome.Error.Should().BeNull();
        outcome.Value!.BuildersClub.Should().BeNull();
        var plan = outcome.Value.Normal!;

        plan.Catalog.Should().Be("NORMAL");
        plan.Pages.Single(x => x.Name == "novelties")
            .Should()
            .Match<CatalogImportPageChange>(x =>
                x.Action == CatalogImportPageChange.ADD
                && x.Path == "/furniture/novelties"
                && x.OffersAdded == 3
            );
        plan.Pages.Single(x => x.Name == "vault").Action.Should().Be(CatalogImportPageChange.ADD);
        plan.Pages.Single(x => x.Name == "secret")
            .Action.Should()
            .Be(CatalogImportPageChange.UNCHANGED);
        plan.MissingClasses.Should().Equal("floor sprite 40");
        plan.Skipped.Should()
            .ContainSingle()
            .Which.Should()
            .Be(
                new CatalogImportSkip(
                    $"/furniture/novelties: offer {UNKNOWN_FURNI} habbo_only",
                    "no furniture here is floor sprite 40",
                    1
                )
            );
        plan.Saved.Should().BeFalse();
        _hotel.Rows().Should().Be(before);
    }

    [Fact]
    public async Task Applying_MakesTheNewPages_WithEveryKindOfOffer_AsOneStep_AndAgainHasNothingToDo()
    {
        var log = await HabboLogAsync(CatalogType.Normal);
        var request = new CatalogLogImportRequest(log, null, false, false);

        var outcome = await Importer.ApplyLogsAsync(Editor, request, Ct);

        outcome.Error.Should().BeNull();
        outcome.Value!.Normal!.Saved.Should().BeTrue();
        _service.History.Undo.Should().ContainSingle();

        await using (var db = await _hotel.Db.CreateDbContextAsync(Ct))
        {
            var novelties = await db.CatalogPages.SingleAsync(x => x.Name == "novelties", Ct);
            var vault = await db.CatalogPages.SingleAsync(x => x.Name == "vault", Ct);

            novelties.ParentEntityId.Should().Be(FURNITURE);
            novelties.Display.Should().Be(CatalogPageDisplay.Regular);
            vault.ParentEntityId.Should().Be(ROOT);
            vault
                .Display.Should()
                .Be(CatalogPageDisplay.Invisible, "it is hidden in Habbo's catalogue");

            var onNovelties = await db
                .CatalogOffers.Include(x => x.Products)
                .Where(x => x.CatalogPageEntityId == novelties.Id)
                .OrderBy(x => x.SortOrder)
                .ToListAsync(Ct);
            onNovelties
                .Select(Gives)
                .Should()
                .Equal($"Floor:{CHAIR}:,Wall:{POSTER}:7", "Badge::ACH_Test1", "Effect::5");
            onNovelties.Select(x => x.CanBundle).Should().Equal(true, false, true);

            // The page here sells the chair as Habbo's does, priced four ways, in Habbo's order
            // after the one Habbo does not show; the credits row is sent as activity points 0.
            (
                await db
                    .CatalogOffers.Where(x => x.CatalogPageEntityId == FURNITURE)
                    .OrderBy(x => x.SortOrder)
                    .Select(x => $"{x.CostCredits}:{x.CostCurrency}:{x.CurrencyTypeId}")
                    .ToListAsync(Ct)
            )
                .Skip(1)
                .Should()
                .Equal("5:0:", $"0:3:{DUCKETS_ROW}", $"0:2:{DIAMONDS_ROW}", $"0:7:{DUCKETS_ROW}");
        }

        var again = (await Importer.PlanLogsAsync(request, Ct)).Value!;
        again.Changes.Should().BeFalse();
        again
            .Normal!.Pages.Should()
            .OnlyContain(x => x.Action == CatalogImportPageChange.UNCHANGED);
        (await Importer.ApplyLogsAsync(Editor, request, Ct))
            .Value!.Normal!.Saved.Should()
            .BeFalse();
        _service.History.Undo.Should().ContainSingle();
    }

    /// <summary>
    /// Habbo's "Lodge", shown in both its catalogues, under the furniture tab, and under it a
    /// page only its Builders Club shows.
    /// </summary>
    private const int LODGE = 60;
    private const int LODGE_EXTRAS = 61;
    private const int LODGE_CHAIR = 206;
    private const int EXTRAS_POSTER = 207;

    private async Task<CatalogLogImportRequest> LodgeLogsAsync()
    {
        _habbo.Db.Insert(Page(LODGE, FURNITURE, "Lodge", CatalogPageDisplay.Both, sort: 6));
        _habbo.Db.Insert(
            Page(LODGE_EXTRAS, LODGE, "Lodge_extras", CatalogPageDisplay.BuildersClubOnly)
        );
        HabboOffer(LODGE_CHAIR, LODGE, "lodge_chair", (ProductType.Floor, CHAIR, null));
        HabboOffer(EXTRAS_POSTER, LODGE_EXTRAS, "extras", (ProductType.Wall, POSTER, "3"));

        return new CatalogLogImportRequest(
            await HabboLogAsync(CatalogType.Normal),
            await HabboLogAsync(CatalogType.BuildersClub),
            false,
            false
        );
    }

    [Fact]
    public async Task APageInBothLogs_IsMadeOnce_ShownInBoth_WithThePagesOnlyTheBuildersClubHasUnderIt()
    {
        var request = await LodgeLogsAsync();

        (await Importer.ApplyLogsAsync(Editor, request, Ct)).Error.Should().BeNull();

        await using (var db = await _hotel.Db.CreateDbContextAsync(Ct))
        {
            var lodge = await db.CatalogPages.SingleAsync(x => x.Name == "lodge", Ct);
            var extras = await db.CatalogPages.SingleAsync(x => x.Name == "lodge_extras", Ct);

            lodge.Display.Should().Be(CatalogPageDisplay.Both);
            lodge.ParentEntityId.Should().Be(FURNITURE);
            extras.Display.Should().Be(CatalogPageDisplay.BuildersClubOnly);
            extras.ParentEntityId.Should().Be(lodge.Id);
            (await db.CatalogOffers.CountAsync(x => x.CatalogPageEntityId == lodge.Id, Ct))
                .Should()
                .Be(1, "the normal catalogue's import says what it sells, once");
        }

        // Both catalogues show the one page, and the Builders Club's lists the page under it.
        var normal = _hotel.NormalProvider();
        var builders = _hotel.BuildersClubProvider();
        await normal.ReloadAsync(Ct);
        await builders.ReloadAsync(Ct);
        normal.Current.PagesById.Values.Count(x => x.Name == "lodge").Should().Be(1);
        builders.Current.PagesById.Values.Count(x => x.Name == "lodge").Should().Be(1);
        builders.Current.PagesById.Values.Should().Contain(x => x.Name == "lodge_extras");

        // Neither catalogue's import undoes the other's order, so again there is nothing to do.
        (await Importer.PlanLogsAsync(request, Ct))
            .Value!.Changes.Should()
            .BeFalse();
    }

    [Fact]
    public async Task ACopyOnlyTheBuildersClubHas_OfAPageInBothLogs_IsTakenOut_AndWhatIsUnderItMoves()
    {
        var request = await LodgeLogsAsync();

        // As imports made before the two were one left them: the normal catalogue's page, and
        // a copy shown only in the Builders Club, with its page under it.
        _hotel.Db.Insert(Page(63, FURNITURE, "Lodge", sort: 5));
        _hotel.Db.Insert(
            Page(LODGE, FURNITURE, "Lodge", CatalogPageDisplay.BuildersClubOnly, sort: 6)
        );
        _hotel.Db.Insert(
            Page(LODGE_EXTRAS, LODGE, "Lodge_extras", CatalogPageDisplay.BuildersClubOnly)
        );

        (await Importer.ApplyLogsAsync(Editor, request, Ct)).Error.Should().BeNull();

        await using var db = await _hotel.Db.CreateDbContextAsync(Ct);
        var lodges = await db.CatalogPages.Where(x => x.Name == "lodge").ToListAsync(Ct);
        var shown = lodges.Single(x => x.Display != CatalogPageDisplay.Invisible);

        shown.Id.Should().Be(63, "the normal catalogue's page is the one to show in both");
        shown.Display.Should().Be(CatalogPageDisplay.Both);
        lodges
            .Single(x => x.Id == LODGE)
            .Display.Should()
            .Be(CatalogPageDisplay.Invisible, "the page is in the Builders Club once");
        (await db.CatalogPages.SingleAsync(x => x.Id == LODGE_EXTRAS, Ct))
            .ParentEntityId.Should()
            .Be(shown.Id);
    }

    [Fact]
    public async Task TheLogsTakenInOneAtATime_StillMakeAPageInBothOnce_UnderTheFurnitureTab()
    {
        // Habbo's front page is its first tab, ahead of the one its Builders Club cuts down.
        _habbo.Db.Insert(Page(62, ROOT, "Front", sort: -1));
        var both = await LodgeLogsAsync();

        // A catalog emptied, so nothing in it leads to the Builders Club yet.
        (await _service.ResetAsync(Editor, false, Ct))
            .Error.Should()
            .BeNull();

        (await Importer.ApplyLogsAsync(Editor, both with { BuildersClubLog = null }, Ct))
            .Error.Should()
            .BeNull();
        (await Importer.ApplyLogsAsync(Editor, both with { NormalLog = null }, Ct))
            .Error.Should()
            .BeNull();

        await using var db = await _hotel.Db.CreateDbContextAsync(Ct);
        var furniture = await db.CatalogPages.SingleAsync(x => x.Name == "furniture", Ct);
        var lodge = await db.CatalogPages.SingleAsync(x => x.Name == "lodge", Ct);
        var builders = await db.CatalogPages.SingleAsync(x => x.Name == "builders", Ct);

        lodge.Display.Should().Be(CatalogPageDisplay.Both);
        lodge.ParentEntityId.Should().Be(furniture.Id);
        builders.ParentEntityId.Should().Be(furniture.Id, "not the front page, the first tab");
        (await db.CatalogPages.SingleAsync(x => x.Name == "lodge_extras", Ct))
            .ParentEntityId.Should()
            .Be(lodge.Id);
        (await db.CatalogPages.CountAsync(x => x.Name == "front", Ct)).Should().Be(1);
    }

    [Fact]
    public async Task TheBuildersClubLogFirst_ThenTheNormal_MakeTheSharedPageOnce()
    {
        var both = await LodgeLogsAsync();

        (await Importer.ApplyLogsAsync(Editor, both with { NormalLog = null }, Ct))
            .Error.Should()
            .BeNull();
        (await Importer.ApplyLogsAsync(Editor, both with { BuildersClubLog = null }, Ct))
            .Error.Should()
            .BeNull();

        await using var db = await _hotel.Db.CreateDbContextAsync(Ct);
        var lodge = await db.CatalogPages.SingleAsync(x => x.Name == "lodge", Ct);

        lodge.Display.Should().Be(CatalogPageDisplay.Both);
        lodge.ParentEntityId.Should().Be(FURNITURE);
        (await db.CatalogPages.SingleAsync(x => x.Name == "lodge_extras", Ct))
            .ParentEntityId.Should()
            .Be(lodge.Id);
        (await db.CatalogOffers.CountAsync(x => x.CatalogPageEntityId == lodge.Id, Ct))
            .Should()
            .Be(1, "the Builders Club's chair offer is the normal catalogue's one");
    }

    [Fact]
    public async Task APage_ComesWithItsHeaderTeaserAndEveryText_EmptySlotsKeptInPlace()
    {
        var page = Page(60, FURNITURE, "Spooky", sort: 6);

        // Habbo leaves a header out as an empty slot, so the teaser stays the second picture.
        page.ImageData = ["", "spromo_spooky"];
        page.TextData = ["Dark magic starts here.", "", "<b>Read me</b>"];
        _habbo.Db.Insert(page);
        HabboOffer(206, 60, "spooky_chair", (ProductType.Floor, CHAIR, null));

        var request = new CatalogLogImportRequest(
            await HabboLogAsync(CatalogType.Normal),
            null,
            false,
            false
        );

        (await Importer.ApplyLogsAsync(Editor, request, Ct)).Error.Should().BeNull();

        await using var db = await _hotel.Db.CreateDbContextAsync(Ct);
        var spooky = await db.CatalogPages.SingleAsync(x => x.Name == "spooky", Ct);

        spooky.ImageData.Should().Equal("", "spromo_spooky");
        spooky.TextData.Should().Equal("Dark magic starts here.", "", "<b>Read me</b>");
    }

    [Fact]
    public async Task ThePlan_NamesThePicturesThisHotelHasNoFileOf()
    {
        var page = Page(60, FURNITURE, "Spooky", sort: 6);

        page.ImageData = ["had_it", "spromo_spooky", ""];
        _habbo.Db.Insert(page);

        using var folder = new Turbo.Tests.Assets.AssetFolder(_hotel.Db);
        var images = new AdminCatalogImages(
            _hotel.Db,
            folder.Store,
            TimeProvider.System,
            new CapturingLogger<AdminCatalogImages>()
        );
        var had = folder.Store.FullPathOf(
            Turbo.Primitives.Gamedata.Enums.AssetBundleKind.CatalogImage,
            "had_it.gif"
        );

        Directory.CreateDirectory(Path.GetDirectoryName(had)!);
        await File.WriteAllBytesAsync(had, "GIF89a"u8.ToArray(), Ct);

        var importer = new AdminCatalogImport(
            _hotel.Db,
            _service,
            new CapturingLogger<AdminCatalogImport>(),
            images
        );
        var plan = (
            await importer.PlanLogsAsync(
                new CatalogLogImportRequest(
                    await HabboLogAsync(CatalogType.Normal),
                    null,
                    false,
                    false
                ),
                Ct
            )
        ).Value!;

        plan.Normal!.MissingPictures.Should().Equal("spromo_spooky");
    }

    [Fact]
    public async Task ARoomBundle_ComesWithEveryFurniAndItsBadge()
    {
        _habbo.Db.Insert(Page(60, FURNITURE, "Room_bundle", sort: 6));

        // As Habbo's room deals are: a badge and more furni than a small offer has.
        HabboOffer(
            206,
            60,
            "roomdeal",
            [
                (ProductType.Badge, null, "DAD11"),
                (ProductType.Floor, CHAIR, null),
                .. Enumerable
                    .Range(1, 30)
                    .Select(n => (ProductType.Wall, (int?)POSTER, (string?)$"{n}")),
            ]
        );

        var request = new CatalogLogImportRequest(
            await HabboLogAsync(CatalogType.Normal),
            null,
            false,
            false
        );

        (await Importer.ApplyLogsAsync(Editor, request, Ct)).Error.Should().BeNull();

        await using var db = await _hotel.Db.CreateDbContextAsync(Ct);
        var bundle = await db
            .CatalogOffers.Include(x => x.Products)
            .SingleAsync(x => x.LocalizationId == "roomdeal", Ct);

        bundle.Products.Should().HaveCount(32);
        bundle.Products!.Should().Contain(x => x.ExtraParam == "DAD11");
        bundle.CanBundle.Should().BeFalse("a badge is given once per purchase");
    }

    [Fact]
    public async Task BothLogs_AreTakenInAsOneStep()
    {
        var request = new CatalogLogImportRequest(
            await HabboLogAsync(CatalogType.Normal),
            await HabboLogAsync(CatalogType.BuildersClub),
            false,
            false
        );

        var plan = (await Importer.ApplyLogsAsync(Editor, request, Ct)).Value!;

        plan.Normal!.Saved.Should().BeTrue();
        plan.BuildersClub!.Catalog.Should().Be("BUILDERS_CLUB");
        plan.BuildersClub.Pages.Single(x => x.Name == "builders")
            .Should()
            .Match<CatalogImportPageChange>(x =>
                x.Action == CatalogImportPageChange.UPDATE && x.OffersAdded == 1
            );
        _service.History.Undo.Should().ContainSingle();

        await using var db = await _hotel.Db.CreateDbContextAsync(Ct);
        (await db.CatalogOffers.CountAsync(x => x.CatalogPageEntityId == BUILDERS_PAGE, Ct))
            .Should()
            .Be(1);
    }

    [Fact]
    public async Task ALogOfTheOtherCatalogue_IsRefusedWithWhy()
    {
        var builders = await HabboLogAsync(CatalogType.BuildersClub);

        var outcome = await Importer.PlanLogsAsync(
            new CatalogLogImportRequest(builders, null, false, false),
            Ct
        );

        outcome.Value.Should().BeNull();
        outcome.Error.Should().Contain("normal catalogue's log has no index of it");
        outcome.Error.Should().Contain("give them as the Builders Club catalogue's log");
    }

    [Fact]
    public async Task APageLaidOutAsThisHotelDoesNotReadIt_IsLeftOutWithANote()
    {
        var packets = PacketLog.Read(await HabboLogAsync(CatalogType.Normal)).ToList();
        var page = packets.FindIndex(x => x.Name == "CatalogPage");
        var pageId = BitConverter.ToInt32(packets[page].Payload!.Take(4).Reverse().ToArray());
        packets[page] = packets[page] with { Payload = [.. packets[page].Payload!, 0, 0, 0, 0] };

        var capture = CatalogPacketLogReader.Read(packets, CatalogType.Normal);

        capture.Root.Should().NotBeNull();
        capture.Pages.Should().NotContainKey(pageId);
        capture.Notes.Should().ContainSingle().Which.Should().Contain("4 bytes are left over");
    }
}
