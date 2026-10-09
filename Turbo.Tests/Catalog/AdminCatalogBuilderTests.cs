using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Catalog;
using Turbo.Admin.Configuration;
using Turbo.Catalog.Editing;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Entities.Gamedata;
using Turbo.Database.Entities.Pets;
using Turbo.Database.Entities.Sound;
using Turbo.Inventory;
using Turbo.Inventory.Configuration;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Sound;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// The catalog editor's page builders: a plan of the offers a kind of page sells, named the way
/// its layout reads them (trophies as gold, silver and bronze, a pet type from the digits ending
/// its name), and applying it through the edit service, which makes only the items asked for and
/// reports the ones it refuses.
/// </summary>
public sealed class AdminCatalogBuilderTests : IDisposable
{
    private static readonly PlayerId Editor = 1;

    private readonly CatalogFixture _catalog = new();
    private readonly CatalogEditService _service;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AdminCatalogBuilderTests()
    {
        _service = new CatalogEditService(
            _catalog.Db,
            _catalog.Definitions,
            _catalog.NormalProvider(),
            _catalog.BuildersClubProvider(),
            _catalog.Fakes.Create<ISessionGateway>(),
            _catalog.Fakes.Create<IGrainFactory>(),
            new CapturingLogger<ICatalogEditService>()
        );
    }

    public void Dispose() => _catalog.Dispose();

    private AdminCatalogBuilder Builder(int itemLimit = 500) =>
        new(
            _catalog.Db,
            _service,
            new GivableEffects(Options.Create(new EffectConfig())),
            Options.Create(new AdminConfig { CatalogBuilderItemLimit = itemLimit })
        );

    private static CatalogBuildRequest Request(
        string builder,
        string? baseName = null,
        string? line = null,
        string? prefix = null,
        string[]? keys = null,
        bool setLayout = false,
        string? display = null
    ) =>
        new(
            builder,
            baseName,
            line,
            prefix,
            null,
            keys,
            CostCredits: 3,
            CostCurrency: 0,
            CurrencyTypeId: null,
            ClubLevel: 0,
            CanGift: true,
            Visible: true,
            setLayout,
            display
        );

    private async Task<CatalogBuildPlan> PreviewAsync(
        CatalogBuildRequest request,
        int pageId = CHILD,
        int itemLimit = 500
    )
    {
        var outcome = await Builder(itemLimit).PreviewAsync(pageId, request, Ct);

        outcome.Error.Should().BeNull();

        return outcome.Value!;
    }

    private async Task<CatalogBuildResponse> ApplyAsync(
        CatalogBuildRequest request,
        int pageId = CHILD
    )
    {
        var outcome = await Builder().ApplyAsync(Editor, pageId, request, Ct);

        outcome.Error.Should().BeNull();

        return outcome.Value!;
    }

    private List<CatalogOfferEntity> OffersOn(int pageId)
    {
        using var db = _catalog.Db.CreateDbContext();

        return
        [
            .. db
                .CatalogOffers.AsNoTracking()
                .Include(x => x.Products)
                .Where(x => x.CatalogPageEntityId == pageId)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id),
        ];
    }

    private void Trophy(int id, string name) =>
        _catalog.AddDefinition(id, name, set: x => x.Logic = TrophyData.LOGIC_NAME);

    private void Product(int id, string code, string name) =>
        _catalog.Db.Insert(
            new GamedataProductEntity
            {
                Id = id,
                Code = code,
                Name = name,
            }
        );

    private void Text(int id, string key, string value) =>
        _catalog.Db.Insert(
            new GamedataTextEntity
            {
                Id = id,
                Key = key,
                Value = value,
            }
        );

    private void SellAlone(int offerId, ProductType type, int? definitionId, string? extraParam)
    {
        _catalog.Db.Insert(
            new CatalogOfferEntity
            {
                Id = offerId,
                CatalogPageEntityId = FURNITURE,
                LocalizationId = "sold",
                CostCredits = 1,
                CostCurrency = 0,
                CanGift = true,
                CanBundle = false,
                ClubLevel = 0,
                Visible = true,
                Page = null!,
            }
        );
        _catalog.Db.Insert(
            new CatalogProductEntity
            {
                Id = offerId,
                CatalogOfferEntityId = offerId,
                ProductType = type,
                FurnitureDefinitionEntityId = definitionId,
                ExtraParam = extraParam,
                Quantity = 1,
                Offer = null!,
            }
        );
    }

    private void SetUpTrophies()
    {
        Trophy(200, "prizetrophy2*1");
        Trophy(201, "prizetrophy2*2");
        Trophy(202, "prizetrophy2*3");
        Trophy(203, "prizetrophy2*4");
        Trophy(204, "greektrophy*3");
        Trophy(205, "greektrophy*1");
        Trophy(206, "greektrophy*2");
        Trophy(207, "cine_star");
        Product(1, "a0 prizetrophy2_g", "Gold trophy");
        Product(2, "a0 prizetrophy2_s", "Silver trophy");
        Product(3, "a0 prizetrophy2_b", "Bronze trophy");
        SellAlone(300, ProductType.Floor, 200, null);
    }

    [Fact]
    public async Task Trophies_GroupEachFamilysFirstThreeColours_AsGoldSilverAndBronze()
    {
        SetUpTrophies();

        var plan = await PreviewAsync(Request(CatalogPageBuilders.TROPHIES));

        plan.Layout.Should().Be("trophies");
        plan.CreatesPages.Should().BeFalse();
        plan.Items.Select(x => (x.Key, x.LocalizationId, x.Note))
            .Should()
            .Equal(
                ("def:207", "cine_star", null),
                ("trophy:greektrophy:g", "greektrophy_g", "gold"),
                ("trophy:greektrophy:s", "greektrophy_s", "silver"),
                ("trophy:greektrophy:b", "greektrophy_b", "bronze"),
                ("trophy:prizetrophy2:g", "a0 prizetrophy2_g", "gold"),
                ("trophy:prizetrophy2:s", "a0 prizetrophy2_s", "silver"),
                ("trophy:prizetrophy2:b", "a0 prizetrophy2_b", "bronze"),
                ("def:203", "prizetrophy2*4", null)
            );
        plan.Items[4].Title.Should().Be("Gold trophy");
        plan.Items[4]
            .Products.Should()
            .Equal(new CatalogBuildProduct("floor", 200, "prizetrophy2*1", null, 1));
        plan.Items.Where(x => x.AlreadyOffered)
            .Select(x => x.Key)
            .Should()
            .Equal("trophy:prizetrophy2:g");
        plan.Items.Select(x => x.Products.Single().DefinitionId)
            .Should()
            .Equal(207, 205, 206, 204, 200, 201, 202, 203);
    }

    [Fact]
    public async Task BuildingOntoANewPage_MakesItUnderThePage_WithTheLayout_AsOneStepToUndo()
    {
        SetUpTrophies();
        var keys = (await PreviewAsync(Request(CatalogPageBuilders.TROPHIES), FURNITURE))
            .Items.Select(x => x.Key)
            .ToArray();

        var outcome = await Builder()
            .ApplyAsync(
                Editor,
                FURNITURE,
                Request(CatalogPageBuilders.TROPHIES, keys: keys, display: "regular") with
                {
                    NewPageTitle = "Trophy shop",
                },
                Ct
            );

        outcome.Error.Should().BeNull();
        var pageId = outcome.Value!.PageId!.Value;

        using (var db = _catalog.Db.CreateDbContext())
        {
            var page = db.CatalogPages.AsNoTracking().Single(x => x.Id == pageId);

            page.ParentEntityId.Should().Be(FURNITURE);
            page.Localization.Should().Be("Trophy shop");
            page.Layout.Should().Be("trophies");
            page.Display.Should().Be(CatalogPageDisplay.Regular);
        }

        OffersOn(pageId).Should().HaveCount(8);
        _service.History.Undo.Should().ContainSingle();

        (await _service.UndoAsync(Editor, Ct)).Error.Should().BeNull();

        using (var db = _catalog.Db.CreateDbContext())
            db.CatalogPages.Any(x => x.Id == pageId).Should().BeFalse();
    }

    [Fact]
    public async Task ApplyingTrophies_CreatesAnOfferEach_InThePlansOrder()
    {
        SetUpTrophies();
        var keys = (await PreviewAsync(Request(CatalogPageBuilders.TROPHIES)))
            .Items.Select(x => x.Key)
            .ToArray();

        var result = await ApplyAsync(
            Request(CatalogPageBuilders.TROPHIES, keys: [.. keys.Reverse()], setLayout: true)
        );

        result.OffersCreated.Should().Be(8);
        result.Failures.Should().BeEmpty();
        result.UnpublishedChanges.Should().Be(9);
        var offers = OffersOn(CHILD);
        offers
            .Select(x => x.LocalizationId)
            .Should()
            .Equal(
                "cine_star",
                "greektrophy_g",
                "greektrophy_s",
                "greektrophy_b",
                "a0 prizetrophy2_g",
                "a0 prizetrophy2_s",
                "a0 prizetrophy2_b",
                "prizetrophy2*4"
            );
        offers.Should().AllSatisfy(x => x.CostCredits.Should().Be(3));
        offers.Should().AllSatisfy(x => x.CanBundle.Should().BeTrue());
        using var db = _catalog.Db.CreateDbContext();
        db.CatalogPages.Single(x => x.Id == CHILD).Layout.Should().Be("trophies");
    }

    [Fact]
    public async Task Pets_AreEveryTypeWithASellablePalette_AndApplyingMakesAPageEach()
    {
        _catalog.Db.Insert(Breed(1, 3, 0, sellable: true));
        _catalog.Db.Insert(Breed(2, 3, 1, sellable: true));
        _catalog.Db.Insert(Breed(3, 7, 0, sellable: false));
        _catalog.Db.Insert(Breed(4, 12, 0, sellable: true));
        Text(1, "pet.type.3", "Turtle");
        Product(1, "a0 pet12", "Dragon and Starter Food");
        SellAlone(300, ProductType.Pet, null, "3");

        var plan = await PreviewAsync(Request(CatalogPageBuilders.PETS));

        plan.Layout.Should().Be("pets");
        plan.CreatesPages.Should().BeTrue();
        plan.Items.Select(x => (x.Key, x.LocalizationId, x.PageTitle, x.AlreadyOffered))
            .Should()
            .Equal(("pet:3", "a0 pet3", "Turtle", true), ("pet:12", "a0 pet12", "Dragon", false));
        plan.Items[0].Note.Should().Be("no product data name");
        plan.Items[1].Products.Should().Equal(new CatalogBuildProduct("pet", null, null, "12", 1));

        var result = await ApplyAsync(
            Request(CatalogPageBuilders.PETS, keys: ["pet:12"], setLayout: true)
        );

        result.Should().BeEquivalentTo(new CatalogBuildResponse(1, 1, 0, 2, [], CHILD));
        using var db = _catalog.Db.CreateDbContext();
        var page = db.CatalogPages.Single(x => x.ParentEntityId == CHILD);
        (page.Localization, page.Layout, page.Display)
            .Should()
            .Be(("Dragon", "pets", CatalogPageDisplay.Invisible));
        var offer = OffersOn(page.Id).Single();
        offer.LocalizationId.Should().Be("a0 pet12");
        offer.CanBundle.Should().BeFalse();
        offer
            .Products!.Single()
            .Should()
            .BeEquivalentTo(
                new
                {
                    ProductType = ProductType.Pet,
                    ExtraParam = "12",
                    Quantity = 1,
                }
            );
        // The page the pets go under keeps its layout.
        db.CatalogPages.Single(x => x.Id == CHILD).Layout.Should().Be("default_3x3");
    }

    private static PetBreedEntity Breed(int id, int type, int palette, bool sellable) =>
        new()
        {
            Id = id,
            TypeId = type,
            PaletteId = palette,
            BreedId = 0,
            Sellable = sellable,
        };

    [Fact]
    public async Task Colours_AreTheFamilyInTheOrderOfItsNumbers()
    {
        _catalog.AddDefinition(200, "chair_plasty*10");
        _catalog.AddDefinition(201, "chair_plasty*2");
        _catalog.AddDefinition(202, "chair_plasty*1");
        _catalog.AddDefinition(203, "chair_plasty_big*1");
        _catalog.AddDefinition(204, "chair_plasty");

        var plan = await PreviewAsync(
            Request(CatalogPageBuilders.COLOURS, baseName: "chair_plasty")
        );

        plan.Layout.Should().Be("default_3x3_color_grouping");
        plan.Items.Select(x => x.LocalizationId)
            .Should()
            .Equal("chair_plasty*1", "chair_plasty*2", "chair_plasty*10");

        var missing = await Builder()
            .PreviewAsync(CHILD, Request(CatalogPageBuilders.COLOURS, baseName: "sofa"), Ct);
        missing.Value.Should().BeNull();
        missing.Error.Should().Contain("sofa*1");
        (await Builder().PreviewAsync(CHILD, Request(CatalogPageBuilders.COLOURS), Ct))
            .Error.Should()
            .NotBeNull();
    }

    [Fact]
    public async Task AFurniLine_IsTheLineExactly_OrTheClassNamesThatStartTheSame()
    {
        _catalog.AddDefinition(200, "pura_b", set: x => x.FurniLine = "pura");
        _catalog.AddDefinition(201, "pura_a", ProductType.Wall, x => x.FurniLine = "pura");
        _catalog.AddDefinition(202, "pure_x", set: x => x.FurniLine = "other");

        var lines = await Builder().GetFurniLinesAsync(Ct);
        lines
            .Lines.Should()
            .Equal(new CatalogFurniLine("other", 1), new CatalogFurniLine("pura", 2));

        var byLine = await PreviewAsync(Request(CatalogPageBuilders.FURNI_LINE, line: "pura"));
        byLine.Layout.Should().Be("default_3x3");
        byLine
            .Items.Select(x => (x.LocalizationId, x.Products.Single().Type))
            .Should()
            .Equal(("pura_a", "wall"), ("pura_b", "floor"));

        var byPrefix = await PreviewAsync(Request(CatalogPageBuilders.FURNI_LINE, prefix: "pur"));
        byPrefix.Items.Select(x => x.LocalizationId).Should().Equal("pura_a", "pura_b", "pure_x");

        (
            await Builder()
                .PreviewAsync(CHILD, Request(CatalogPageBuilders.FURNI_LINE, prefix: "pu"), Ct)
        )
            .Error.Should()
            .Contain("3 characters");
    }

    [Fact]
    public async Task PetCustomization_IsTheItemsForAPetType()
    {
        _catalog.AddDefinition(
            200,
            "horse_dye_2",
            set: x =>
            {
                x.FurniCategory = FurnitureCategory.PetShampoo;
                x.CustomParams = "15 Gold";
            }
        );
        _catalog.AddDefinition(
            201,
            "dog_saddle",
            set: x =>
            {
                x.FurniCategory = FurnitureCategory.PetSaddle;
                x.CustomParams = "0 1";
            }
        );
        _catalog.AddDefinition(
            202,
            "horse_dye_1",
            set: x =>
            {
                x.FurniCategory = FurnitureCategory.PetShampoo;
                x.CustomParams = "15 Red";
            }
        );

        var all = await PreviewAsync(Request(CatalogPageBuilders.PET_CUSTOMIZATION));
        all.Layout.Should().Be("petcustomization");
        all.Items.Select(x => x.LocalizationId)
            .Should()
            .Equal("dog_saddle", "horse_dye_1", "horse_dye_2");

        var horses = await PreviewAsync(
            Request(CatalogPageBuilders.PET_CUSTOMIZATION) with
            {
                PetType = 15,
            }
        );
        horses.Items.Select(x => x.LocalizationId).Should().Equal("horse_dye_1", "horse_dye_2");
    }

    [Fact]
    public async Task Effects_AreTheNamedOnesAPlayerMayOwn()
    {
        Product(1, "avatar_effect12", "Torch");
        Product(2, "avatar_effect28", "Swimming");
        Product(3, "avatar_effect20000", "Past the last");
        Product(4, "avatar_effect12_dt", "Torch for a day");
        Text(1, "fx_5", "Hearts");
        Text(2, "fx_12_desc", "A torch");
        SellAlone(300, ProductType.Effect, null, "12");

        var plan = await PreviewAsync(Request(CatalogPageBuilders.EFFECTS));

        plan.Layout.Should().Be("pixeleffects");
        plan.Items.Select(x => (x.Key, x.Title, x.LocalizationId, x.AlreadyOffered, x.Note))
            .Should()
            .Equal(
                ("fx:5", "Hearts", "avatar_effect5", false, "no product data name"),
                ("fx:12", "Torch", "avatar_effect12", true, null)
            );
        plan.Items[1]
            .Products.Should()
            .Equal(new CatalogBuildProduct("effect", null, null, "12", 1));
    }

    [Fact]
    public async Task SoldLimited_MovesOnlyTheSoldOutLimitedOffersFromOtherPages()
    {
        _catalog.AddOffer(110, CHILD, credits: 5);
        _catalog.Db.Insert(Series(1, SOLD, remaining: 0));
        _catalog.Db.Insert(Series(2, IN_DUCKETS, remaining: 5));
        _catalog.Db.Insert(Series(3, 110, remaining: 0));

        var plan = await PreviewAsync(Request(CatalogPageBuilders.SOLD_LIMITED));

        plan.Layout.Should().Be("sold_ltd_items");
        plan.Items.Should().ContainSingle();
        plan.Items[0]
            .Should()
            .BeEquivalentTo(
                new
                {
                    Key = $"offer:{SOLD}",
                    OfferId = SOLD,
                    AlreadyOffered = false,
                }
            );

        var result = await ApplyAsync(
            Request(
                CatalogPageBuilders.SOLD_LIMITED,
                keys: [$"offer:{SOLD}", $"offer:{IN_DUCKETS}"]
            )
        );

        result.OffersMoved.Should().Be(1);
        result.OffersCreated.Should().Be(0);
        result.Failures.Select(x => x.Key).Should().Equal($"offer:{IN_DUCKETS}");
        OffersOn(CHILD).Select(x => x.Id).Should().Equal(110, SOLD);
        OffersOn(FURNITURE).Select(x => x.Id).Should().NotContain(SOLD);
    }

    private static LtdSeriesEntity Series(int id, int productId, int remaining) =>
        new()
        {
            Id = id,
            CatalogProductEntityId = productId,
            TotalQuantity = 10,
            RemainingQuantity = remaining,
            RaffleWindowSeconds = 0,
            IsActive = true,
        };

    [Fact]
    public async Task Applying_MakesOnlyTheKeysAskedFor_AndReportsTheOnesItCannot()
    {
        _catalog.AddDefinition(200, "pura_a", set: x => x.FurniLine = "pura");
        _catalog.AddDefinition(201, "pura_b", set: x => x.FurniLine = "pura");

        var result = await ApplyAsync(
            Request(CatalogPageBuilders.FURNI_LINE, line: "pura", keys: ["def:201", "def:999"])
        );

        result.OffersCreated.Should().Be(1);
        result
            .Failures.Should()
            .Equal(new CatalogBuildFailure("def:999", "That is not in the plan any more."));
        OffersOn(CHILD).Select(x => x.LocalizationId).Should().Equal("pura_b");

        // What the edit service refuses is a failure of that item, and the rest go on.
        var refused = await ApplyAsync(
            Request(CatalogPageBuilders.FURNI_LINE, line: "pura", keys: ["def:200"]) with
            {
                CostCredits = -1,
            }
        );

        refused.OffersCreated.Should().Be(0);
        refused
            .Failures.Should()
            .Equal(new CatalogBuildFailure("def:200", "A price is 0 or more."));
    }

    [Fact]
    public async Task ABuilder_ThatIsNotOne_OrAPageThatIsGone_IsRefused()
    {
        (await Builder().PreviewAsync(CHILD, Request("everything"), Ct))
            .Error.Should()
            .StartWith("Choose a builder");
        (await Builder().PreviewAsync(999, Request(CatalogPageBuilders.EFFECTS), Ct))
            .Error.Should()
            .Be("That page is gone.");
        (
            await Builder()
                .ApplyAsync(
                    Editor,
                    CHILD,
                    Request(CatalogPageBuilders.PETS, keys: ["pet:1"], display: "sideways"),
                    Ct
                )
        )
            .Error.Should()
            .NotBeNull();
    }

    private void RoomPaper(int id, string name, FurnitureCategory category) =>
        _catalog.AddDefinition(id, name, ProductType.Wall, x => x.FurniCategory = category);

    private void SellWithParam(int offerId, int definitionId, string extraParam) =>
        SellAlone(offerId, ProductType.Wall, definitionId, extraParam);

    [Fact]
    public async Task Spaces_AreEachPattern_FloorsThenWallpapersThenLandscapes_InNumberOrder()
    {
        RoomPaper(200, "floor", FurnitureCategory.Floor);
        RoomPaper(201, "wallpaper", FurnitureCategory.WallPaper);
        RoomPaper(202, "landscape", FurnitureCategory.Landscape);
        Product(1, "wallpaper_single_1.10", "Wallpaper");
        Product(2, "wallpaper_single_1.2", "Wallpaper");
        Product(3, "floor_single_110", "Floor Pattern");
        Product(4, "floor_single_12", "Floor Pattern");
        Product(5, "landscape_single_3", "Landscape");
        Product(6, "floor_single_", "Nothing after it");
        SellWithParam(300, 201, "1.2");

        var plan = await PreviewAsync(Request(CatalogPageBuilders.SPACES));

        plan.Layout.Should().Be("spaces_new");
        plan.Warnings.Should().BeEmpty();
        plan.Items.Select(x => (x.Key, x.LocalizationId, x.Title, x.Note, x.AlreadyOffered))
            .Should()
            .Equal(
                ("space:floor_single_12", "floor_single_12", "Floor Pattern 12", "floor", false),
                ("space:floor_single_110", "floor_single_110", "Floor Pattern 110", "floor", false),
                (
                    "space:wallpaper_single_1.2",
                    "wallpaper_single_1.2",
                    "Wallpaper 1.2",
                    "wallpaper",
                    true
                ),
                (
                    "space:wallpaper_single_1.10",
                    "wallpaper_single_1.10",
                    "Wallpaper 1.10",
                    "wallpaper",
                    false
                ),
                (
                    "space:landscape_single_3",
                    "landscape_single_3",
                    "Landscape 3",
                    "landscape",
                    false
                )
            );
        plan.Items[2]
            .Products.Should()
            .Equal(new CatalogBuildProduct("wall", 201, "wallpaper", "1.2", 1));

        var result = await ApplyAsync(
            Request(
                CatalogPageBuilders.SPACES,
                keys: ["space:landscape_single_3", "space:floor_single_12"],
                setLayout: true
            )
        );

        result.OffersCreated.Should().Be(2);
        var offers = OffersOn(CHILD);
        offers
            .Select(x =>
                (
                    x.LocalizationId,
                    x.Products!.Single().FurnitureDefinitionEntityId,
                    x.Products!.Single().ExtraParam
                )
            )
            .Should()
            .Equal(("floor_single_12", 200, "12"), ("landscape_single_3", 202, "3"));
    }

    [Fact]
    public async Task Spaces_WarnWhenAPaperOrItsPatternsAreMissing()
    {
        RoomPaper(200, "floor", FurnitureCategory.Floor);
        RoomPaper(201, "wallpaper", FurnitureCategory.WallPaper);
        Product(1, "floor_single_1", "Floor Pattern");
        Product(2, "landscape_single_1", "Landscape");

        var plan = await PreviewAsync(Request(CatalogPageBuilders.SPACES));

        plan.Items.Select(x => x.Key).Should().Equal("space:floor_single_1");
        plan.Warnings.Should().HaveCount(2);
        plan.Warnings[0].Should().StartWith("There are no wallpaper patterns");
        plan.Warnings[1].Should().StartWith("There is no landscape item");
    }

    [Fact]
    public async Task Posters_AreEachPosterId_InNumberOrder()
    {
        // The fixture's wall item is the one poster definition.
        Product(1, "poster 10", "Lapland Poster");
        Product(2, "poster 2", "Ancestress");
        Product(3, "poster 1000", "Comedy Poster");
        SellWithParam(300, POSTER, "10");

        var plan = await PreviewAsync(Request(CatalogPageBuilders.POSTERS));

        plan.Layout.Should().Be("default_3x3");
        plan.Items.Select(x => (x.Key, x.LocalizationId, x.Title, x.AlreadyOffered))
            .Should()
            .Equal(
                ("poster:2", "poster 2", "Ancestress", false),
                ("poster:10", "poster 10", "Lapland Poster", true),
                ("poster:1000", "poster 1000", "Comedy Poster", false)
            );

        var result = await ApplyAsync(Request(CatalogPageBuilders.POSTERS, keys: ["poster:1000"]));

        result.OffersCreated.Should().Be(1);
        OffersOn(CHILD)
            .Single()
            .Products!.Single()
            .Should()
            .BeEquivalentTo(new { FurnitureDefinitionEntityId = POSTER, ExtraParam = "1000" });

        using (var db = _catalog.Db.CreateDbContext())
            db.FurnitureDefinitions.Where(x => x.Id == POSTER)
                .ExecuteUpdate(x => x.SetProperty(d => d.Name, "poster_gone"));

        (await PreviewAsync(Request(CatalogPageBuilders.POSTERS)))
            .Warnings.Should()
            .Equal(
                "There is no poster item, so there are no posters to sell.",
                "There is nothing to build."
            );
    }

    [Fact]
    public async Task BadgeDisplays_AreTheBadgeDisplayLogic_OrByNameUntilItIsMapped()
    {
        _catalog.AddDefinition(200, "badge_display2");
        _catalog.AddDefinition(201, "badge_display");
        Product(1, "a0 badge_display", "Badge Display");
        SellAlone(300, ProductType.Floor, 200, null);

        var byName = await PreviewAsync(Request(CatalogPageBuilders.BADGE_DISPLAYS));

        byName.Layout.Should().Be("badge_display");
        byName
            .Items.Select(x => (x.Key, x.LocalizationId, x.Title, x.AlreadyOffered))
            .Should()
            .Equal(
                ("def:201", "a0 badge_display", "Badge Display", false),
                ("def:200", "badge_display2", "def:200", true)
            );

        _catalog.AddDefinition(202, "trophy_case", set: x => x.Logic = BadgeDisplayData.LOGIC_NAME);

        var byLogic = await PreviewAsync(Request(CatalogPageBuilders.BADGE_DISPLAYS));
        byLogic.Items.Select(x => x.Key).Should().Equal("def:202");

        (await ApplyAsync(Request(CatalogPageBuilders.BADGE_DISPLAYS, keys: ["def:202"])))
            .OffersCreated.Should()
            .Be(1);
        OffersOn(CHILD).Single().LocalizationId.Should().Be("trophy_case");
    }

    [Fact]
    public async Task AFurniLine_SkipsTheItemsThatNeedAPattern()
    {
        _catalog.AddDefinition(
            200,
            "wallpaper",
            ProductType.Wall,
            x =>
            {
                x.FurniCategory = FurnitureCategory.WallPaper;
                x.FurniLine = "basic";
            }
        );
        _catalog.AddDefinition(
            201,
            "poster",
            ProductType.Wall,
            x =>
            {
                x.FurniCategory = FurnitureCategory.Poster;
                x.FurniLine = "basic";
            }
        );
        _catalog.AddDefinition(202, "basic_chair", set: x => x.FurniLine = "basic");

        var plan = await PreviewAsync(Request(CatalogPageBuilders.FURNI_LINE, line: "basic"));

        plan.Items.Select(x => x.Key).Should().Equal("def:202");
        plan.Warnings.Should()
            .Equal("Spaces and posters need a pattern: use the Spaces or Posters builder.");

        var result = await ApplyAsync(
            Request(CatalogPageBuilders.FURNI_LINE, line: "basic", keys: ["def:200", "def:202"])
        );

        result.OffersCreated.Should().Be(1);
        result.Failures.Select(x => x.Key).Should().Equal("def:200");
        OffersOn(CHILD).Select(x => x.LocalizationId).Should().Equal("basic_chair");
    }

    private void Song(int id, string name, string? code, bool official) =>
        _catalog.Db.Insert(
            new SongEntity
            {
                Id = id,
                Code = code,
                Name = name,
                Author = "Teemu",
                Track = "1:0,4",
                LengthSeconds = 4,
                IsOfficial = official,
            }
        );

    private void SongDisk(int id = 200) =>
        _catalog.AddDefinition(
            id,
            "song_disk",
            set: x =>
            {
                x.FurniCategory = FurnitureCategory.TraxSong;
                x.Logic = SongDisks.LOGIC;
            }
        );

    [Fact]
    public async Task SongDiscs_AreADiskOfEachOfficialSong_ByName()
    {
        SongDisk();
        Song(1, "Xmas Magic", "Xmas11", official: true);
        Song(2, "Alley Cat", null, official: true);
        Song(3, "Homemade", null, official: false);
        Product(1, "SONG Xmas11", "Xmas Magic");
        SellAlone(300, ProductType.Floor, 200, "2");

        var plan = await PreviewAsync(Request(CatalogPageBuilders.SONG_DISCS));

        plan.Layout.Should().Be("soundmachine");
        plan.Warnings.Should().BeEmpty();
        plan.Items.Select(x => (x.Key, x.LocalizationId, x.Title, x.AlreadyOffered))
            .Should()
            .Equal(
                ("song:2", "song_disk", "Alley Cat by Teemu", true),
                ("song:1", "SONG Xmas11", "Xmas Magic by Teemu", false)
            );
        plan.Items[1]
            .Products.Should()
            .Equal(new CatalogBuildProduct("floor", 200, "song_disk", "1", 1));

        var result = await ApplyAsync(Request(CatalogPageBuilders.SONG_DISCS, keys: ["song:1"]));

        result.OffersCreated.Should().Be(1);
        var offer = OffersOn(CHILD).Single();
        offer.LocalizationId.Should().Be("SONG Xmas11");
        offer
            .Products!.Single()
            .Should()
            .BeEquivalentTo(
                new
                {
                    FurnitureDefinitionEntityId = 200,
                    ExtraParam = "1",
                    Quantity = 1,
                }
            );
    }

    [Fact]
    public async Task SongDiscs_AreEverySong_WhileNoneIsOfficial_AndNothingWithoutADisk()
    {
        Song(1, "Homemade", null, official: false);

        var noDisk = await PreviewAsync(Request(CatalogPageBuilders.SONG_DISCS));
        noDisk.Items.Should().BeEmpty();
        noDisk.Warnings[0].Should().StartWith("There is no song disk item");

        SongDisk();
        var plan = await PreviewAsync(Request(CatalogPageBuilders.SONG_DISCS));

        plan.Items.Select(x => x.Key).Should().Equal("song:1");
        plan.Warnings.Should().Equal("No song is marked official; showing every song.");
    }

    [Fact]
    public async Task APreview_ListsUpToItsLimit_AndSaysSo()
    {
        _catalog.AddDefinition(200, "pura_a", set: x => x.FurniLine = "pura");
        _catalog.AddDefinition(201, "pura_b", set: x => x.FurniLine = "pura");
        _catalog.AddDefinition(202, "pura_c", set: x => x.FurniLine = "pura");

        var plan = await PreviewAsync(
            Request(CatalogPageBuilders.FURNI_LINE, line: "pura"),
            itemLimit: 2
        );

        plan.Items.Should().HaveCount(2);
        plan.Warnings.Should().ContainSingle().Which.Should().Contain("first 2 of 3");
    }
}
