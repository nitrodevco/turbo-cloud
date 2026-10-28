using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Turbo.Furniture.Custody;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Trading;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Inventory;

public sealed class FurnitureTradeCustodyTests : IDisposable
{
    private const int Initiator = 8;
    private const int Partner = 9;
    private const int Chair = 1;
    private readonly FurnitureWorld _world = new();
    private readonly FurnitureCustody _custody;

    public FurnitureTradeCustodyTests()
    {
        _world.AddPlayer(Initiator, "initiator");
        _world.AddPlayer(Partner, "partner");
        _world.AddDefinition(Chair, "chair", "default_floor");
        _custody = TestCustody.Over(
            _world.Db,
            _world.NewRecorderAsync(Ct).GetAwaiter().GetResult()
        );
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _world.Dispose();

    [Fact]
    public async Task ExchangeCommitsOnceAndReplaysTheSameReceipt()
    {
        var first = Snapshot(Initiator, 101, null);
        var second = Snapshot(Partner, 102, null);
        _world.AddFurni(101, Initiator, Chair, null);
        _world.AddFurni(102, Partner, Chair, null);
        var cause = MovementCause.New(MovementCauses.TRADE, Partner).StartingAt(4);
        var request = Request(first, second);

        (await _custody.ExchangeTradeAsync(request, cause, Ct)).Should().BeTrue();
        (await _custody.ExchangeTradeAsync(request, cause, Ct)).Should().BeTrue();

        _world.Row(101).PlayerEntityId.Should().Be(Partner);
        _world.Row(102).PlayerEntityId.Should().Be(Initiator);
        _world
            .Movements()
            .Where(x => x.OperationId == cause.OperationId)
            .Select(x => (x.Seq, x.AssetId, x.FromOwner, x.ToOwner))
            .Should()
            .Equal((4, 101L, Initiator, Partner), (5, 102L, Partner, Initiator));
        await using var db = await _world.Db.CreateDbContextAsync(Ct);
        db.FurnitureTradeReceipts.Should().ContainSingle(x => x.OperationId == cause.OperationId);
    }

    [Fact]
    public async Task ReusedOperationWithChangedExtraDataThrowsWithoutMovingItems()
    {
        var item = Snapshot(Initiator, 101, "Exact");
        _world.AddFurni(101, Initiator, Chair, null);
        await using (var db = await _world.Db.CreateDbContextAsync(Ct))
        {
            await db
                .Furnitures.Where(x => x.Id == 101)
                .ExecuteUpdateAsync(up => up.SetProperty(x => x.ExtraData, "Exact"), Ct);
        }
        var cause = MovementCause.New(MovementCauses.TRADE, Initiator);
        (await _custody.ExchangeTradeAsync(Request(item, null), cause, Ct)).Should().BeTrue();

        var changed = Snapshot(Initiator, 101, "exact");
        var act = () => _custody.ExchangeTradeAsync(Request(changed, null), cause, Ct);
        await act.Should().ThrowAsync<InvalidOperationException>();
        _world.Row(101).PlayerEntityId.Should().Be(Partner);
    }

    [Fact]
    public async Task RefusesItemsThatAreNotInTheOwnersInventory()
    {
        var item = Snapshot(Initiator, 101, null);
        _world.AddFurni(101, Partner, Chair, null);
        var cause = MovementCause.New(MovementCauses.TRADE, Initiator);
        (await _custody.ExchangeTradeAsync(Request(item, null), cause, Ct)).Should().BeFalse();
        _world.Row(101).PlayerEntityId.Should().Be(Partner);
        _world.Movements().Should().BeEmpty();
    }

    [Fact]
    public async Task CurrentDefinitionGuardCanRefuseAfterTheFurnitureIsRead()
    {
        _world.AddFurni(101, Initiator, Chair, null);
        var item = Snapshot(Initiator, 101, null);
        await using (var db = await _world.Db.CreateDbContextAsync(Ct))
        {
            await db
                .FurnitureDefinitions.Where(x => x.Id == Chair)
                .ExecuteUpdateAsync(up => up.SetProperty(x => x.CanTrade, false), Ct);
        }

        (
            await _custody.ExchangeTradeAsync(
                Request(item, null),
                MovementCause.New(MovementCauses.TRADE, Initiator),
                Ct
            )
        )
            .Should()
            .BeFalse();
        _world.Row(101).PlayerEntityId.Should().Be(Initiator);
        _world.Movements().Should().BeEmpty();
    }

    [Fact]
    public async Task RecordingOffStillWritesReceiptAndAnEmptyOfferIsAllowed()
    {
        _world.Config.Set(new() { Enabled = false });
        var item = Snapshot(Initiator, 101, null);
        _world.AddFurni(101, Initiator, Chair, null);
        var cause = MovementCause.New(MovementCauses.TRADE, Initiator);
        (await _custody.ExchangeTradeAsync(Request(item, null), cause, Ct)).Should().BeTrue();
        _world.Movements().Should().BeEmpty();
        await using var db = await _world.Db.CreateDbContextAsync(Ct);
        db.FurnitureTradeReceipts.Should().ContainSingle(x => x.OperationId == cause.OperationId);
    }

    [Fact]
    public async Task A_second_side_tradability_refusal_rolls_back_the_first_update_and_receipt()
    {
        _world.AddDefinition(2, "otherchair", "default_floor");
        _world.AddFurni(101, Initiator, Chair, null);
        _world.AddFurni(102, Partner, 2, null);
        var second = Snapshot(Partner, 102, null);
        second = second with { Definition = second.Definition with { Id = 2, SpriteId = 2 } };
        await using (var db = await _world.Db.CreateDbContextAsync(Ct))
            await db
                .FurnitureDefinitions.Where(x => x.Id == 2)
                .ExecuteUpdateAsync(x => x.SetProperty(d => d.CanTrade, false), Ct);

        (
            await _custody.ExchangeTradeAsync(
                Request(Snapshot(Initiator, 101, null), second),
                MovementCause.New(MovementCauses.TRADE, Partner),
                Ct
            )
        )
            .Should()
            .BeFalse();

        _world.Rows().Select(x => x.PlayerEntityId).Should().Equal(Initiator, Partner);
        _world.Movements().Should().BeEmpty();
        await using var read = await _world.Db.CreateDbContextAsync(Ct);
        (await read.FurnitureTradeReceipts.AnyAsync(Ct)).Should().BeFalse();
    }

    [Fact]
    public async Task DuplicateIdsAndSelfTradeAreRejected()
    {
        var item = Snapshot(Initiator, 101, null);
        var duplicate = new FurnitureTradeRequest
        {
            InitiatorId = new(Initiator),
            PartnerId = new(Partner),
            InitiatorItems = [item],
            PartnerItems = [Snapshot(Partner, 101, null)],
        };
        var cause = MovementCause.New(MovementCauses.TRADE, Initiator);
        var duplicateCall = () => _custody.ExchangeTradeAsync(duplicate, cause, Ct);
        await duplicateCall.Should().ThrowAsync<ArgumentException>();

        var self = Request(item, null) with { PartnerId = Initiator };
        var selfCall = () => _custody.ExchangeTradeAsync(self, cause, Ct);
        await selfCall.Should().ThrowAsync<ArgumentException>();
    }

    private static FurnitureTradeRequest Request(
        Turbo.Primitives.Inventory.Snapshots.FurnitureItemSnapshot? first,
        Turbo.Primitives.Inventory.Snapshots.FurnitureItemSnapshot? second
    ) =>
        new()
        {
            InitiatorId = new(Initiator),
            PartnerId = new(Partner),
            InitiatorItems = first is null
                ? ImmutableArray<Turbo.Primitives.Inventory.Snapshots.FurnitureItemSnapshot>.Empty
                : [first],
            PartnerItems = second is null
                ? ImmutableArray<Turbo.Primitives.Inventory.Snapshots.FurnitureItemSnapshot>.Empty
                : [second],
        };

    private static Turbo.Primitives.Inventory.Snapshots.FurnitureItemSnapshot Snapshot(
        int owner,
        int id,
        string? extraData
    )
    {
        // Get the complete definition snapshot through the fixture's canonical mapper.
        var definition = new Turbo.Primitives.Furniture.Snapshots.FurnitureDefinitionSnapshot
        {
            Id = Chair,
            SpriteId = Chair,
            Name = "chair",
            ProductType = Turbo.Primitives.Furniture.Enums.ProductType.Floor,
            FurniCategory = Turbo.Primitives.Furniture.Enums.FurnitureCategory.Default,
            LogicName = "default_floor",
            TotalStates = 0,
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
            UsagePolicy = Turbo.Primitives.Rooms.Enums.FurnitureUsageType.Nobody,
            ExtraData = null,
        };
        return new()
        {
            ItemId = id,
            SpriteId = Chair,
            OwnerId = owner,
            OwnerName = "player",
            Definition = definition,
            StuffData = new Turbo.Primitives.Furniture.Snapshots.StuffData.EmptyStuffSnapshot
            {
                StuffBitmask = 0,
            },
            ExtraData = extraData ?? "{}",
            SecondsToExpiration = -1,
            HasRentPeriodStarted = false,
            RoomId = 0,
        };
    }
}
