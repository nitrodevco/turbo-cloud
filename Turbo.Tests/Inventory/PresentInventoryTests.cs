using System.Reflection;
using System.Text.Json;
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
using Turbo.Primitives.Furniture.ExtraData;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Inventory;

/// <summary>
/// A gift as the receiving inventory keeps it: the present is listed with its tag and drawn in
/// the box and ribbon chosen, while the item inside stays out of every list until the present
/// is opened, whoever then holds it.
/// </summary>
public sealed class PresentInventoryTests : IDisposable
{
    private const int RECEIVER = 8;
    private const int TRADED_TO = 9;

    private const int CHAIR = 1;
    private const int PRESENT = 2;
    private const int TROPHY = 3;

    private static readonly BindingFlags All =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly List<FurnitureDefinitionEntity> _definitions = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PresentInventoryTests()
    {
        AddPlayer(RECEIVER, "receiver");
        AddPlayer(TRADED_TO, "other");
        AddDefinition(CHAIR, "chair", "default_floor");
        AddDefinition(PRESENT, "present_wrap*1", PresentData.LOGIC_NAME);
        AddDefinition(TROPHY, "trophy_gold", TrophyData.LOGIC_NAME);

        _fakes.Handlers["TryGetDefinition"] = call =>
            _definitions.FirstOrDefault(x => x.Id == (int)call.Args[0]!)?.ToSnapshot(0.01);
        _fakes.Handlers["GetPlayerNameAsync"] = _ => Task.FromResult<string?>("receiver");
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task OnlyThePresentIsListed_WithItsTagAndItsBoxAndRibbon()
    {
        await NewInventory(RECEIVER).ReceivePresentAsync(Gift(), Ct);

        foreach (var inventory in new[] { NewInventory(RECEIVER), NewInventory(RECEIVER) })
        {
            var present = (await inventory.GetAllItemSnapshotsAsync(Ct)).Single();

            present.Definition.Id.Should().Be(PRESENT);
            present.Extra.Should().Be(3005, "box 3, ribbon 5, as the client splits it");
            present
                .StuffData.Should()
                .BeOfType<MapStuffSnapshot>()
                .Which.Data.Should()
                .Contain(PresentData.MESSAGE, "Happy birthday")
                .And.Contain(PresentData.PRODUCT_CODE, "chair")
                .And.Contain(PresentData.PURCHASER_NAME, "buyer")
                .And.Contain(PresentData.PURCHASER_FIGURE, "hd-180-1");
        }

        var rows = await Rows();

        rows.Should().HaveCount(2);
        rows.Single(x => x.FurnitureDefinitionEntityId == CHAIR)
            .ChestItemEntityId.Should()
            .Be(rows.Single(x => x.FurnitureDefinitionEntityId == PRESENT).Id);
    }

    [Fact]
    public async Task AnAnonymousGift_HasNoNameOnItsTag()
    {
        await NewInventory(RECEIVER)
            .ReceivePresentAsync(Gift() with { PurchaserName = null, PurchaserFigure = null }, Ct);

        var tag = (MapStuffSnapshot)
            (await NewInventory(RECEIVER).GetAllItemSnapshotsAsync(Ct)).Single().StuffData;

        tag.Data.Should().NotContainKey(PresentData.PURCHASER_NAME);
        tag.Data.Should().NotContainKey(PresentData.PURCHASER_FIGURE);
    }

    [Fact]
    public async Task Unwrapping_ListsWhatWasInside()
    {
        var inventory = NewInventory(RECEIVER);

        await inventory.ReceivePresentAsync(Gift(), Ct);

        var present = (await inventory.GetAllItemSnapshotsAsync(Ct)).Single();
        var inside = await inventory.UnwrapPresentAsync(present.ItemId, Ct);

        inside.Should().NotBeNull();
        inside!.Definition.Id.Should().Be(CHAIR);
        (await inventory.GetAllItemSnapshotsAsync(Ct))
            .Select(x => x.ItemId)
            .Should()
            .Contain(inside.ItemId);
        (await NewInventory(RECEIVER).GetAllItemSnapshotsAsync(Ct))
            .Select(x => x.ItemId)
            .Should()
            .Contain(inside.ItemId, "the row is free of the present");
        (await inventory.UnwrapPresentAsync(present.ItemId, Ct))
            .Should()
            .BeNull("it holds nothing any more");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AGiftedTrophy_IsEngravedWithWhoBoughtIt_NotWhoReceivedIt(bool namedOnTag)
    {
        var inventory = NewInventory(RECEIVER);
        var gift = Gift() with
        {
            Product = Gift().Product with { FurniDefinitionId = TROPHY, SpriteId = TROPHY },
            ExtraParam = "Well done",
            PurchaserName = namedOnTag ? "buyer" : null,
            PurchaserFigure = namedOnTag ? "hd-180-1" : null,
        };

        await inventory.ReceivePresentAsync(gift, Ct);

        var present = (await inventory.GetAllItemSnapshotsAsync(Ct)).Single();
        var trophy = await inventory.UnwrapPresentAsync(present.ItemId, Ct);

        var engraving = trophy!
            .StuffData.Should()
            .BeOfType<LegacyStuffSnapshot>()
            .Which.Data.Split(TrophyData.SEPARATOR);

        engraving[0].Should().Be("buyer");
        engraving[2].Should().Be("Well done");
    }

    [Fact]
    public async Task ATrophyBoughtForOneself_IsStillEngravedWithItsOwner()
    {
        var inventory = NewInventory(RECEIVER);
        var offer = new CatalogOfferSnapshot
        {
            Id = 1,
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
            ProductIds = [1],
            Products = [Gift().Product with { FurniDefinitionId = TROPHY, SpriteId = TROPHY }],
        };

        await inventory.GrantCatalogOfferAsync(offer, "Mine", 1, Ct);

        ((LegacyStuffSnapshot)(await inventory.GetAllItemSnapshotsAsync(Ct)).Single().StuffData)
            .Data.Split(TrophyData.SEPARATOR)[0]
            .Should()
            .Be("receiver");
    }

    [Fact]
    public async Task APresentThatChangedHands_OpensIntoItsNewOwnersInventory()
    {
        await NewInventory(RECEIVER).ReceivePresentAsync(Gift(), Ct);

        var presentId = (await NewInventory(RECEIVER).GetAllItemSnapshotsAsync(Ct)).Single().ItemId;
        var inside = await NewInventory(TRADED_TO).UnwrapPresentAsync(presentId, Ct);

        inside.Should().NotBeNull();
        (await Rows()).Single(x => x.Id == inside!.ItemId).PlayerEntityId.Should().Be(TRADED_TO);
    }

    [Fact]
    public async Task AStaffGift_HasOnlyItsNoteOnTheTag_AndKeepsItsBadgeOutOfTheClientsSight()
    {
        var inventory = NewInventory(RECEIVER);

        await inventory.ReceiveStaffPresentAsync(StaffGift(), Ct);

        var present = (await inventory.GetAllItemSnapshotsAsync(Ct)).Single();
        var tag = present.StuffData.Should().BeOfType<MapStuffSnapshot>().Subject.Data;

        present.Definition.Id.Should().Be(PRESENT);
        tag.Should()
            .Contain(PresentData.MESSAGE, "Thanks for playing Habbo.")
            .And.Contain(PresentData.PRODUCT_CODE, "chair");
        tag.Should()
            .NotContainKeys(
                PresentData.PURCHASER_NAME,
                PresentData.PURCHASER_FIGURE,
                PresentData.TRUSTED_SENDER
            );
        tag.Values.Should().NotContain("ADM", "the badge is the server's to give");

        var row = (await Rows()).Single(x => x.Id == present.ItemId);
        var storage = JsonDocument
            .Parse(row.ExtraData!)
            .RootElement.GetProperty(PresentStorage.SECTION)
            .Deserialize<PresentStorage>();

        storage!.BadgeCode.Should().Be("ADM");
        (await inventory.UnwrapPresentAsync(present.ItemId, Ct))!.Definition.Id.Should().Be(CHAIR);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AStaffGift_SaysItsSenderIsTrusted_OnlyWhenAsked(bool trusted)
    {
        await NewInventory(RECEIVER)
            .ReceiveStaffPresentAsync(StaffGift() with { TrustedSender = trusted }, Ct);

        var tag = (MapStuffSnapshot)
            (await NewInventory(RECEIVER).GetAllItemSnapshotsAsync(Ct)).Single().StuffData;

        if (trusted)
            tag.Data.Should().Contain(PresentData.TRUSTED_SENDER, "true");
        else
            tag.Data.Should().NotContainKey(PresentData.TRUSTED_SENDER);
    }

    private static StaffPresentGrantRequest StaffGift() =>
        new()
        {
            FurniDefinitionId = CHAIR,
            PresentDefinitionId = PRESENT,
            Message = "Thanks for playing Habbo.",
            BadgeCode = "ADM",
            TrustedSender = false,
        };

    private async Task<List<FurnitureEntity>> Rows()
    {
        await using var db = await _db.CreateDbContextAsync(Ct);

        return await db.Furnitures.AsNoTracking().ToListAsync(Ct);
    }

    private static PresentGrantRequest Gift() =>
        new()
        {
            Product = new CatalogProductSnapshot
            {
                Id = 1,
                OfferId = 1,
                ProductType = ProductType.Floor,
                FurniDefinitionId = CHAIR,
                SpriteId = CHAIR,
                ExtraParam = null,
                Quantity = 1,
                UniqueSize = 0,
                UniqueRemaining = 0,
                ClassName = null,
            },
            ExtraParam = string.Empty,
            PresentDefinitionId = PRESENT,
            BoxType = 3,
            RibbonType = 5,
            Message = "Happy birthday",
            PurchaserName = "buyer",
            PurchaserFigure = "hd-180-1",
            BuyerName = "buyer",
        };

    private void AddPlayer(int id, string name) =>
        _db.Insert(
            new PlayerEntity
            {
                Id = id,
                Name = name,
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );

    private void AddDefinition(int id, string name, string logic)
    {
        var definition = new FurnitureDefinitionEntity
        {
            Id = id,
            SpriteId = id,
            Name = name,
            ProductType = ProductType.Floor,
            FurniCategory = FurnitureCategory.Default,
            Logic = logic,
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

    /// <summary>
    /// A player's inventory grain, freshly activated, with its real furniture section: the
    /// loader and stuff data as the host builds them, over this database.
    /// </summary>
    private IInventoryGrain NewInventory(int playerId)
    {
        var grain = GrainHarness.Create(
            typeof(InventoryModule).Assembly,
            "Turbo.Inventory.Grains.InventoryGrain",
            _fakes,
            _db,
            playerId
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
