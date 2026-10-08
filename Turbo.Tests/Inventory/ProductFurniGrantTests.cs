using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Players;
using Turbo.Database.Extensions;
using Turbo.Furniture.Providers;
using Turbo.Inventory;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Texts;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Inventory;

/// <summary>
/// Furni bought as what a parameter names keeps it on the item: a paper's pattern, a poster's
/// id and a song disc's song as the product names them, a badge display's badge as the buyer
/// picked it. The inventory lists it so before and after the row is read back, and a paper used
/// in a room is used up.
/// </summary>
public sealed class ProductFurniGrantTests : IDisposable
{
    private const int BUYER = 7;
    private const int OFFER_ID = 900;

    private static readonly BindingFlags All =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly List<FurnitureDefinitionEntity> _definitions = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ProductFurniGrantTests()
    {
        _db.Insert(
            new PlayerEntity
            {
                Id = BUYER,
                Name = "buyer",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );

        AddDefinition(1, "wallpaper", ProductType.Wall, FurnitureCategory.WallPaper);
        AddDefinition(2, "floor", ProductType.Wall, FurnitureCategory.Floor);
        AddDefinition(3, "landscape", ProductType.Wall, FurnitureCategory.Landscape);
        AddDefinition(4, "poster", ProductType.Wall, FurnitureCategory.Poster);
        AddDefinition(5, "song_disk", ProductType.Floor, FurnitureCategory.TraxSong);
        AddDefinition(
            6,
            "badge_display",
            ProductType.Floor,
            FurnitureCategory.Default,
            BadgeDisplayData.LOGIC_NAME
        );
        AddDefinition(7, "chair", ProductType.Floor, FurnitureCategory.Default);

        _fakes.Handlers["TryGetDefinition"] = call =>
            _definitions.FirstOrDefault(x => x.Id == (int)call.Args[0]!)?.ToSnapshot(0.01);
        _fakes.Handlers["GetPlayerNameAsync"] = _ => Task.FromResult<string?>("buyer");
    }

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData(1, "101")]
    [InlineData(2, "203")]
    [InlineData(3, "1.1")]
    [InlineData(4, "12")]
    [InlineData(5, "5")]
    public async Task WhatTheProductNames_IsTheItemsData_NotWhatTheClientSent(
        int definitionId,
        string productParam
    )
    {
        await NewInventory()
            .GrantCatalogOfferAsync(Offer(definitionId, productParam), "999", 2, Ct);

        var listed = await NewInventory().GetAllItemSnapshotsAsync(Ct);

        listed.Should().HaveCount(2);
        listed
            .Select(x => x.StuffData)
            .Should()
            .AllBeOfType<LegacyStuffSnapshot>()
            .Which.Select(x => x.Data)
            .Should()
            .AllBe(productParam);
    }

    [Fact]
    public async Task ASongDisc_NamesItsSong()
    {
        await NewInventory().GrantCatalogOfferAsync(Offer(5, "5"), string.Empty, 1, Ct);

        var disc = (await NewInventory().GetAllItemSnapshotsAsync(Ct)).Single();

        ProductStuffData.TryGetSongId(disc.StuffData, out var songId).Should().BeTrue();
        songId.Should().Be(5);
    }

    [Fact]
    public async Task OtherFurni_IsNotGivenTheClientsParameter()
    {
        await NewInventory().GrantCatalogOfferAsync(Offer(7, "x"), "999", 1, Ct);

        (await NewInventory().GetAllItemSnapshotsAsync(Ct))
            .Single()
            .StuffData.Should()
            .BeOfType<LegacyStuffSnapshot>()
            .Which.Data.Should()
            .Be("0");
    }

    [Fact]
    public async Task ABadgeDisplay_ShowsTheChosenBadge_TheBuyerAndTheDate_AfterAReloadToo()
    {
        var inventory = NewInventory();

        await inventory.GrantCatalogOfferAsync(Offer(6, null), " ADM ", 1, Ct);

        string[] expected = ["0", "ADM", "buyer", ClientDates.Format(DateTime.UtcNow)];

        (await inventory.GetAllItemSnapshotsAsync(Ct))
            .Single()
            .StuffData.Should()
            .BeOfType<StringStuffSnapshot>()
            .Which.Data.Should()
            .Equal(expected);
        (await NewInventory().GetAllItemSnapshotsAsync(Ct))
            .Single()
            .StuffData.Should()
            .BeOfType<StringStuffSnapshot>()
            .Which.Data.Should()
            .Equal(expected, "the row read back is listed the same");
    }

    [Fact]
    public async Task UsingUpAnItem_DeletesItsRow_AndTakesItOffTheList()
    {
        await NewInventory().GrantCatalogOfferAsync(Offer(1, "101"), string.Empty, 1, Ct);

        var inventory = NewInventory();
        var paper = (await inventory.GetAllItemSnapshotsAsync(Ct)).Single();

        var consumed = await inventory.ConsumeFurnitureAsync(paper.ItemId, Ct);

        consumed.Should().NotBeNull();
        consumed!.StuffData.Should().Be(paper.StuffData);
        (await inventory.GetAllItemSnapshotsAsync(Ct)).Should().BeEmpty();
        _fakes.Log.Of("SendComposerAsync").Should().NotBeEmpty("the client is told it is gone");

        await using var db = await _db.CreateDbContextAsync(Ct);

        (await db.Furnitures.CountAsync(Ct)).Should().Be(0);
        (await inventory.ConsumeFurnitureAsync(paper.ItemId, Ct))
            .Should()
            .BeNull("it is not held any more");
    }

    private void AddDefinition(
        int id,
        string name,
        ProductType type,
        FurnitureCategory category,
        string? logic = null
    )
    {
        var definition = new FurnitureDefinitionEntity
        {
            Id = id,
            SpriteId = id,
            Name = name,
            ProductType = type,
            FurniCategory = category,
            Logic = logic ?? (type == ProductType.Wall ? "default_wall" : "default_floor"),
            Width = 1,
            Length = 1,
            StackHeight = 1,
            CanStack = true,
            CanWalk = false,
            CanSit = false,
            CanLay = false,
            CanRecycle = true,
            CanTrade = true,
            CanGroup = true,
            CanSell = true,
        };

        _definitions.Add(definition);
        _db.Insert(definition);
    }

    private static CatalogOfferSnapshot Offer(int definitionId, string? productParam)
    {
        var product = new CatalogProductSnapshot
        {
            Id = OFFER_ID,
            OfferId = OFFER_ID,
            ProductType = definitionId <= 4 ? ProductType.Wall : ProductType.Floor,
            FurniDefinitionId = definitionId,
            SpriteId = definitionId,
            ExtraParam = productParam,
            Quantity = 1,
            UniqueSize = 0,
            UniqueRemaining = 0,
            ClassName = null,
        };

        return new CatalogOfferSnapshot
        {
            Id = OFFER_ID,
            PageId = 1,
            LocalizationId = "offer",
            Rentable = false,
            CostCredits = 1,
            CostSilver = 0,
            CostCurrency = 0,
            ActivityPointType = null,
            CanGift = true,
            CanBundle = true,
            ClubLevel = 0,
            Visible = true,
            ProductIds = [product.Id],
            Products = [product],
        };
    }

    /// <summary>
    /// The buyer's inventory grain, freshly activated, with its real furniture section: the
    /// loader and stuff data as the host builds them, over this database.
    /// </summary>
    private IInventoryGrain NewInventory()
    {
        var grain = GrainHarness.Create(
            typeof(InventoryModule).Assembly,
            "Turbo.Inventory.Grains.InventoryGrain",
            _fakes,
            _db,
            BUYER
        );
        var definitions = _fakes.Create<IFurnitureDefinitionProvider>();
        var loader = Activator.CreateInstance(
            typeof(InventoryModule).Assembly.GetType(
                "Turbo.Inventory.Factories.InventoryFurnitureLoader"
            )!,
            All,
            null,
            [
                _db,
                definitions,
                new StuffDataFactory(NullLogger<IStuffDataFactory>.Instance),
                NullLogger<IInventoryFurnitureLoader>.Instance,
            ],
            null
        )!;
        var module = Activator.CreateInstance(
            typeof(InventoryModule).Assembly.GetType(
                "Turbo.Inventory.Grains.Modules.InventoryFurniModule"
            )!,
            All,
            null,
            [
                grain,
                RoomHarness.GetMember(grain, "_state")!,
                _db,
                loader,
                definitions,
                _fakes.Create<Turbo.Primitives.Catalog.ICatalogService>(),
                NullLogger.Instance,
            ],
            null
        )!;

        RoomHarness.SetField(grain, "FurniModule", module);

        return (IInventoryGrain)grain;
    }
}
