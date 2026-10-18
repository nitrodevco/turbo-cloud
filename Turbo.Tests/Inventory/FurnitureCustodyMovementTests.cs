using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Turbo.Database.Configuration;
using Turbo.Database.Entities.Furniture;
using Turbo.Database.Entities.Movements;
using Turbo.Database.Entities.Players;
using Turbo.Database.Extensions;
using Turbo.Database.Movements;
using Turbo.Furniture.Providers;
using Turbo.Inventory;
using Turbo.Primitives.Action;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Inventory.Factories;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Inventory;

/// <summary>
/// The inventory side of the furniture custody (docs/movements.md "The inventory"): while
/// recording, every furni an inventory makes, uses up, wraps, unwraps or hands over is one
/// movement under the caller's cause, numbered from its first seq and committed with the row;
/// a refused change and a recording that is off write none, and the rows change as before.
/// </summary>
public sealed class FurnitureCustodyMovementTests : IDisposable
{
    private const int OWNER = 8;
    private const int OTHER = 9;
    private const int ROOM = 5;

    private const int CHAIR = 1;
    private const int PRESENT = 2;
    private const int TELEPORT = 3;

    private static readonly BindingFlags All =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private readonly SqliteDb _db = new();
    private readonly Fakes _fakes = new();
    private readonly TestOptionsMonitor<MovementsConfig> _config = new(new() { Enabled = true });
    private readonly List<FurnitureDefinitionEntity> _definitions = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public FurnitureCustodyMovementTests()
    {
        AddPlayer(OWNER, "owner");
        AddPlayer(OTHER, "other");
        AddDefinition(CHAIR, "chair", "default_floor");
        AddDefinition(PRESENT, "present_wrap*1", PresentData.LOGIC_NAME);
        AddDefinition(TELEPORT, "teleport_door", TeleportFurniture.LOGIC_NAME);

        _fakes.Handlers["TryGetDefinition"] = call =>
            _definitions.FirstOrDefault(x => x.Id == (int)call.Args[0]!)?.ToSnapshot(0.01);
        _fakes.Handlers["GetPlayerNameAsync"] = _ => Task.FromResult<string?>("owner");
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task AGrant_IsMadeFromNowhereIntoTheInventory_UnderTheCallersCause()
    {
        var inventory = await NewInventoryAsync(OWNER);
        var cause = MovementCause.New(MovementCauses.STAFF_GIVE_ITEM, 1, "ref").StartingAt(3);

        var chair = await inventory.GrantFurnitureAsync(CHAIR, "{\"x\":1}", cause, Ct);

        var row = Movements().Should().ContainSingle().Subject;
        row.OperationId.Should().Be(cause.OperationId);
        row.Seq.Should().Be(3, "the caller's first seq");
        row.Cause.Should().Be(MovementCauses.STAFF_GIVE_ITEM);
        row.CauseRef.Should().Be("ref");
        row.Actor.Should().Be(1);
        (row.AssetKind, row.AssetId, row.DefinitionId)
            .Should()
            .Be((AssetKind.Furni, chair!.ItemId.Value, CHAIR));
        (row.FromKind, row.ToKind, row.ToOwner)
            .Should()
            .Be((PlaceKind.None, PlaceKind.Inventory, OWNER));
        row.SnapshotExtraData.Should().Be("{\"x\":1}", "a creation carries what made it");
    }

    [Fact]
    public async Task AnOfferWithATeleporter_NumbersThePlainFurniFirst_ThenBothHalvesOfThePair()
    {
        var inventory = await NewInventoryAsync(OWNER);
        var cause = MovementCause.New(MovementCauses.CATALOG_PURCHASE, OWNER, 77).StartingAt(1);

        var granted = await inventory.GrantCatalogOfferAsync(
            Offer(CHAIR, TELEPORT),
            string.Empty,
            1,
            cause,
            Ct
        );

        granted.Should().HaveCount(3);
        Movements()
            .Select(x => (x.Seq, (int)x.AssetId, x.DefinitionId))
            .Should()
            .Equal(
                (1, granted[0].ItemId.Value, CHAIR),
                (2, granted[1].ItemId.Value, TELEPORT),
                (3, granted[2].ItemId.Value, TELEPORT)
            );
        Movements()
            .Skip(1)
            .Select(x => x.SnapshotExtraData)
            .Should()
            .OnlyContain(x => x!.Contains("room_linker"), "each half is recorded linked");
    }

    [Fact]
    public async Task UsingUp_TakesTheItemFromTheInventoryToNowhere_WithWhatItWas()
    {
        var inventory = await NewInventoryAsync(OWNER);
        var chair = await inventory.GrantFurnitureAsync(CHAIR, "{\"s\":2}", Cause(), Ct);
        var cause = MovementCause.New(MovementCauses.USE_DECORATION, OWNER, ROOM);

        (await inventory.ConsumeFurnitureAsync(chair!.ItemId, cause, Ct)).Should().NotBeNull();

        var row = Movements()
            .Should()
            .ContainSingle(x => x.OperationId == cause.OperationId)
            .Subject;
        (row.FromKind, row.FromOwner, row.ToKind)
            .Should()
            .Be((PlaceKind.Inventory, OWNER, PlaceKind.None));
        row.SnapshotExtraData.Should().Be("{\"s\":2}");
        Rows().Should().BeEmpty();
    }

    [Fact]
    public async Task UsingUpAnItemWithNoRow_IsRefused_AndRecordsNothing()
    {
        var inventory = await NewInventoryAsync(OWNER);
        var chair = await inventory.GrantFurnitureAsync(CHAIR, null, Cause(), Ct);
        await DeleteRowAsync(chair!.ItemId.Value);
        var cause = MovementCause.New(MovementCauses.USE_DECORATION, OWNER);

        (await inventory.ConsumeFurnitureAsync(chair.ItemId, cause, Ct)).Should().BeNull();

        Movements().Should().NotContain(x => x.OperationId == cause.OperationId);
    }

    [Fact]
    public async Task APresent_IsMadeIntoTheInventory_ItsContentIntoThePresent_AndOpenedByWhoeverHoldsIt()
    {
        var inventory = await NewInventoryAsync(OWNER);
        var wrap = MovementCause.New(MovementCauses.STAFF_GIFT, 1);

        var present = await inventory.ReceiveStaffPresentAsync(StaffGift(), wrap, Ct);

        var wrapped = Movements();
        wrapped
            .Select(x => (x.Seq, x.DefinitionId, x.ToKind, x.ToPlace))
            .Should()
            .Equal(
                (0, PRESENT, PlaceKind.Inventory, 0L),
                (1, CHAIR, PlaceKind.Holder, (long)present.ItemId.Value)
            );

        // Traded on, then opened by the other player: the content comes out to the opener.
        (await inventory.TransferFurnitureAsync([present.ItemId], OTHER, Cause(), Ct))
            .Should()
            .BeTrue();
        var open = MovementCause.New(MovementCauses.USE_PRESENT, OTHER, present.ItemId.Value);

        var inside = await (await NewInventoryAsync(OTHER)).UnwrapPresentAsync(
            present.ItemId,
            open,
            Ct
        );

        var opened = Movements()
            .Should()
            .ContainSingle(x => x.OperationId == open.OperationId)
            .Subject;
        opened.AssetId.Should().Be(inside!.ItemId.Value);
        (opened.FromKind, opened.FromOwner, opened.FromPlace)
            .Should()
            .Be((PlaceKind.Holder, OWNER, present.ItemId.Value), "it was wrapped for the owner");
        (opened.ToKind, opened.ToOwner).Should().Be((PlaceKind.Inventory, OTHER));
    }

    [Fact]
    public async Task ATransfer_MovesEachItemFromOneInventoryToTheOther()
    {
        var inventory = await NewInventoryAsync(OWNER);
        var first = await inventory.GrantFurnitureAsync(CHAIR, null, Cause(), Ct);
        var second = await inventory.GrantFurnitureAsync(CHAIR, null, Cause(), Ct);
        var cause = MovementCause.New(MovementCauses.TRADE, OWNER).StartingAt(4);

        (await inventory.TransferFurnitureAsync([first!.ItemId, second!.ItemId], OTHER, cause, Ct))
            .Should()
            .BeTrue();

        Movements()
            .Where(x => x.OperationId == cause.OperationId)
            .Select(x => (x.Seq, x.FromOwner, x.ToOwner, x.FromKind, x.ToKind))
            .Should()
            .Equal(
                (4, OWNER, OTHER, PlaceKind.Inventory, PlaceKind.Inventory),
                (5, OWNER, OTHER, PlaceKind.Inventory, PlaceKind.Inventory)
            );
    }

    [Fact]
    public async Task ATradeWhoseSecondHalfFails_GivesTheFirstHalfBack_AllUnderOneOperation()
    {
        var initiator = await NewInventoryAsync(OWNER);
        var partner = await NewInventoryAsync(OTHER);
        var offered = await initiator.GrantFurnitureAsync(CHAIR, null, Cause(), Ct);
        var asked = await partner.GrantFurnitureAsync(CHAIR, null, Cause(), Ct);
        _fakes.Instances[(typeof(IInventoryGrain), (long)OWNER)] = initiator;
        _fakes.Instances[(typeof(IInventoryGrain), (long)OTHER)] = partner;
        var trades = NewTradeGrain();
        var initiatorCtx = ActionContext.CreateForPlayer(OWNER, ROOM);
        var partnerCtx = ActionContext.CreateForPlayer(OTHER, ROOM);

        (await trades.OpenAsync(initiatorCtx, 2, Ct)).Should().BeTrue();
        (await trades.AddItemsAsync(initiatorCtx, [offered!.ItemId], Ct)).Should().BeTrue();
        (await trades.AddItemsAsync(partnerCtx, [asked!.ItemId], Ct)).Should().BeTrue();
        await trades.AcceptAsync(initiatorCtx, true, Ct);
        await trades.AcceptAsync(partnerCtx, true, Ct);
        // The partner's row goes behind the trade's back, so the second half cannot move.
        await DeleteRowAsync(asked.ItemId.Value);
        await trades.ConfirmAsync(initiatorCtx, true, Ct);
        await trades.ConfirmAsync(partnerCtx, true, Ct);

        var trade = Movements().Where(x => x.Cause == MovementCauses.TRADE).ToList();
        trade
            .Select(x => x.OperationId)
            .Distinct()
            .Should()
            .ContainSingle("one trade, one operation");
        trade
            .Select(x => (x.Seq, (int)x.AssetId, x.FromOwner, x.ToOwner, x.CauseRef))
            .Should()
            .Equal(
                (0, offered.ItemId.Value, OWNER, OTHER, null),
                (2, offered.ItemId.Value, OTHER, OWNER, MovementCauses.TRADE_REF_COMPENSATION)
            );
        trade.Should().OnlyContain(x => x.Actor == OTHER, "the partner's confirm closed it");
        Rows().Single(x => x.Id == offered.ItemId.Value).PlayerEntityId.Should().Be(OWNER);
    }

    [Fact]
    public async Task RecordingOff_WritesNoMovements_AndTheRowsChangeAsBefore()
    {
        _config.Set(new() { Enabled = false });
        var inventory = await NewInventoryAsync(OWNER);

        var chair = await inventory.GrantFurnitureAsync(CHAIR, null, Cause(), Ct);
        var present = await inventory.ReceiveStaffPresentAsync(StaffGift(), Cause(), Ct);
        await inventory.TransferFurnitureAsync([present.ItemId], OTHER, Cause(), Ct);
        await inventory.ConsumeFurnitureAsync(chair!.ItemId, Cause(), Ct);

        Movements().Should().BeEmpty();
        Rows()
            .Select(x => (x.FurnitureDefinitionEntityId, x.PlayerEntityId))
            .Should()
            .BeEquivalentTo([(PRESENT, OTHER), (CHAIR, OWNER)]);
    }

    private static MovementCause Cause() => MovementCause.New("test", 0);

    private static StaffPresentGrantRequest StaffGift() =>
        new()
        {
            FurniDefinitionId = CHAIR,
            PresentDefinitionId = PRESENT,
            Message = "for you",
            BadgeCode = null,
            TrustedSender = false,
        };

    private static CatalogOfferSnapshot Offer(params int[] definitionIds)
    {
        var products = definitionIds
            .Select(
                (definitionId, i) =>
                    new CatalogProductSnapshot
                    {
                        Id = i + 1,
                        OfferId = 77,
                        ProductType = ProductType.Floor,
                        FurniDefinitionId = definitionId,
                        SpriteId = definitionId,
                        ExtraParam = null,
                        Quantity = 1,
                        UniqueSize = 0,
                        UniqueRemaining = 0,
                        ClassName = null,
                    }
            )
            .ToImmutableArray();

        return new CatalogOfferSnapshot
        {
            Id = 77,
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
            ProductIds = [.. products.Select(x => x.Id)],
            Products = products,
        };
    }

    private async Task DeleteRowAsync(int id)
    {
        await using var db = await _db.CreateDbContextAsync(Ct);

        await db.Furnitures.Where(x => x.Id == id).ExecuteDeleteAsync(Ct);
    }

    private List<FurnitureEntity> Rows()
    {
        using var db = _db.CreateDbContext();

        return db.Furnitures.AsNoTracking().OrderBy(x => x.Id).ToList();
    }

    private List<AssetMovementOutboxEntity> Movements()
    {
        using var db = _db.CreateDbContext();

        return db
            .AssetMovementOutbox.AsNoTracking()
            .Where(x =>
                x.Cause != MovementCauses.RECORDING_STARTED
                && x.Cause != MovementCauses.RECORDING_STOPPED
            )
            .OrderBy(x => x.Id)
            .ToList();
    }

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
    /// A player's inventory grain, freshly activated, with its real furniture section and the
    /// custody over this database, recording through the real recorder.
    /// </summary>
    private async Task<IInventoryGrain> NewInventoryAsync(int playerId)
    {
        var recorder = new AssetMovementRecorder(
            _config,
            _db,
            NullLogger<AssetMovementRecorder>.Instance
        );
        await recorder.StartAsync(Ct);

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
                TestCustody.Over(_db, recorder),
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

    private IRoomTradeGrain NewTradeGrain()
    {
        _fakes.Handlers[nameof(IRoomGrain.GetTradePartiesAsync)] = _ =>
            Task.FromResult<TradePartiesSnapshot?>(
                new TradePartiesSnapshot
                {
                    InitiatorId = OWNER,
                    PartnerId = OTHER,
                    PartnerName = "other",
                    InitiatorMayTrade = true,
                    PartnerMayTrade = true,
                }
            );
        _fakes.Handlers["HasAsync"] = _ => Task.FromResult(true);

        var grain = GrainHarness.Create(
            typeof(RoomModule).Assembly,
            "Turbo.Rooms.Grains.RoomTradeGrain",
            _fakes
        );
        RoomHarness.SetMember(
            RoomHarness.GetField(grain, "_state")!,
            "RoomId",
            (Turbo.Primitives.Rooms.RoomId)ROOM
        );

        return (IRoomTradeGrain)grain;
    }
}
