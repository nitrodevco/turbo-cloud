using System.Collections.Immutable;
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
using Turbo.Rooms.Grains;
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

    private readonly FurnitureWorld _world = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public FurnitureCustodyMovementTests()
    {
        _world.AddPlayer(OWNER, "owner");
        _world.AddPlayer(OTHER, "other");
        _world.AddDefinition(CHAIR, "chair", "default_floor");
        _world.AddDefinition(PRESENT, "present_wrap*1", PresentData.LOGIC_NAME);
        _world.AddDefinition(TELEPORT, "teleport_door", TeleportFurniture.LOGIC_NAME);
    }

    public void Dispose() => _world.Dispose();

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
        await _world.DeleteRowAsync(chair!.ItemId.Value, Ct);
        var cause = MovementCause.New(MovementCauses.USE_DECORATION, OWNER);

        (await inventory.ConsumeFurnitureAsync(chair.ItemId, cause, Ct)).Should().BeNull();

        Movements().Should().NotContain(x => x.OperationId == cause.OperationId);
    }

    [Fact]
    public async Task UsingUpSeveralItems_IsAllOrNone_UnderOneOperation()
    {
        var inventory = await NewInventoryAsync(OWNER);
        var first = await inventory.GrantFurnitureAsync(CHAIR, null, Cause(), Ct);
        var second = await inventory.GrantFurnitureAsync(CHAIR, null, Cause(), Ct);
        var gone = await inventory.GrantFurnitureAsync(CHAIR, null, Cause(), Ct);
        await _world.DeleteRowAsync(gone!.ItemId.Value, Ct);
        var refused = MovementCause.New(MovementCauses.USE_CRAFTING, OWNER);

        (await inventory.ConsumeAllFurnitureAsync([first!.ItemId, gone.ItemId], refused, Ct))
            .Should()
            .BeEmpty("one of them has no row");
        Rows().Select(x => x.Id).Should().Contain(first.ItemId.Value, "none was used up");
        Movements().Should().NotContain(x => x.OperationId == refused.OperationId);

        var used = MovementCause.New(MovementCauses.USE_CRAFTING, OWNER);

        (await inventory.ConsumeAllFurnitureAsync([first.ItemId, second!.ItemId], used, Ct))
            .Select(x => x.ItemId)
            .Should()
            .Equal(first.ItemId, second.ItemId);
        Rows().Select(x => x.Id).Should().NotContain([first.ItemId.Value, second.ItemId.Value]);
        Movements()
            .Where(x => x.OperationId == used.OperationId)
            .Select(x => (int)x.Seq)
            .Should()
            .Equal(0, 1);
    }

    [Fact]
    public async Task APresent_IsMadeIntoTheInventory_ItsContentIntoThePresent_AndOpenedByWhoeverHoldsIt()
    {
        var inventory = await NewInventoryAsync(OWNER);
        var wrap = MovementCause.New(MovementCauses.STAFF_GIFT, 1);

        var present = await inventory.ReceiveStaffPresentAsync(StaffGift(), wrap, Ct);

        // The sender is the wrap's actor, the receiver owns the present and its content, and the
        // content is held by the present: a gift is traceable from its wrap to its opening.
        var wrapped = Movements();
        wrapped
            .Select(x => (x.Seq, x.DefinitionId, x.Actor, x.ToKind, x.ToOwner, x.ToPlace))
            .Should()
            .Equal(
                (0, PRESENT, 1, PlaceKind.Inventory, OWNER, 0L),
                (1, CHAIR, 1, PlaceKind.Holder, OWNER, (long)present.ItemId.Value)
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
        _world.Fakes.Instances[(typeof(IInventoryGrain), (long)OWNER)] = initiator;
        _world.Fakes.Instances[(typeof(IInventoryGrain), (long)OTHER)] = partner;
        var trades = NewTradeGrain();
        var initiatorCtx = ActionContext.CreateForPlayer(OWNER, ROOM);
        var partnerCtx = ActionContext.CreateForPlayer(OTHER, ROOM);

        (await trades.OpenAsync(initiatorCtx, 2, Ct)).Should().BeTrue();
        (await trades.AddItemsAsync(initiatorCtx, [offered!.ItemId], Ct)).Should().BeTrue();
        (await trades.AddItemsAsync(partnerCtx, [asked!.ItemId], Ct)).Should().BeTrue();
        await trades.AcceptAsync(initiatorCtx, true, Ct);
        await trades.AcceptAsync(partnerCtx, true, Ct);
        // The partner's row goes behind the trade's back, so the second half cannot move.
        await _world.DeleteRowAsync(asked.ItemId.Value, Ct);
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
                (0, offered.ItemId.Value, OWNER, OTHER, ROOM.ToString()),
                (2, offered.ItemId.Value, OTHER, OWNER, MovementCauses.TRADE_REF_COMPENSATION)
            );
        trade.Should().OnlyContain(x => x.Actor == OTHER, "the partner's confirm closed it");
        Rows().Single(x => x.Id == offered.ItemId.Value).PlayerEntityId.Should().Be(OWNER);
    }

    [Fact]
    public async Task RecordingOff_WritesNoMovements_AndTheRowsChangeAsBefore()
    {
        _world.Config.Set(new() { Enabled = false });
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

    private Task<IInventoryGrain> NewInventoryAsync(int playerId) =>
        _world.NewInventoryAsync(playerId, Ct);

    private List<FurnitureEntity> Rows() => _world.Rows();

    private List<AssetMovementOutboxEntity> Movements() => _world.Movements();

    private IRoomTradeGrain NewTradeGrain()
    {
        _world.Fakes.Handlers[nameof(IRoomGrain.GetTradePartiesAsync)] = _ =>
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
        _world.Fakes.Handlers["HasAsync"] = _ => Task.FromResult(true);

        var grain = GrainHarness.Create<RoomTradeGrain>(_world.Fakes);
        GrainHarness.Replace(grain, new RoomTradeLiveState { RoomId = ROOM });

        return (IRoomTradeGrain)grain;
    }
}
