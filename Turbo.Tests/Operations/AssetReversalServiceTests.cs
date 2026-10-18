using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans;
using Turbo.Database.Entities.Movements;
using Turbo.Operations.Movements;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Operations;

/// <summary>
/// The reversal API (docs/movements.md "Reversing a movement"): a furni moves back through its
/// inventory grain only if it is still where the caller says, once per operation id, for an
/// actor holding <c>items.reverse</c>, as a movement of its own whose cause ref is the operation
/// undone; a borrowed furni is refused, and a refused reversal moves nothing and keeps no receipt.
/// </summary>
public sealed class AssetReversalServiceTests : IDisposable
{
    private const int GIVER = 1;
    private const int TAKER = 2;
    private const int STAFF = 3;
    private const int ROOM = 5;
    private const int CHAIR = 10;
    private const int ITEM = 100;
    private const string TRADE_OPERATION = "0199f0aa000070008000000000000001";

    private readonly FurnitureWorld _world = new();
    private readonly HashSet<long> _holders = [STAFF];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public AssetReversalServiceTests()
    {
        _world.AddPlayer(GIVER, "giver");
        _world.AddPlayer(TAKER, "taker");
        _world.AddPlayer(STAFF, "staff");
        _world.AddDefinition(CHAIR, "chair", "default_floor");
        _world.Fakes.Handlers["HasAsync"] = call =>
            Task.FromResult(
                (string)call.Args[0]! == PermissionNodes.Items.REVERSE
                    && _holders.Contains((long)call.Key!)
            );
    }

    public void Dispose() => _world.Dispose();

    [Fact]
    public async Task AFurniStillWhereExpected_GoesBack_AsOneMovementNamingTheOperationUndone()
    {
        _world.AddFurni(ITEM, TAKER, CHAIR, roomId: null);
        var service = await NewServiceAsync();

        var result = await service.MoveAsync(GiveBack("rev-1"), Ct);

        result.Should().Be(new ReversalResult(ReversalOutcome.Applied, ITEM));
        _world.Row(ITEM).PlayerEntityId.Should().Be(GIVER);
        var row = _world.Movements().Should().ContainSingle().Subject;
        row.OperationId.Should().Be("rev-1");
        row.Cause.Should().Be(MovementCauses.REVERSAL);
        row.CauseRef.Should().Be(TRADE_OPERATION);
        row.Actor.Should().Be(STAFF);
        (row.AssetKind, row.AssetId).Should().Be((AssetKind.Furni, ITEM));
        (row.FromOwner, row.FromKind).Should().Be((TAKER, PlaceKind.Inventory));
        (row.ToOwner, row.ToKind).Should().Be((GIVER, PlaceKind.Inventory));
        Receipts()
            .Should()
            .ContainSingle()
            .Which.Should()
            .Match<AssetReversalReceiptEntity>(x =>
                x.Outcome == ReversalOutcome.Applied
                && x.Reason == "duped trade"
                && x.Actor == STAFF
            );
    }

    [Fact]
    public async Task AFurniThatMovedSince_IsRefused_AndNothingMovesOrIsKept()
    {
        _world.AddRoom(ROOM, TAKER);
        // The taker placed it in a room after the trade.
        _world.AddFurni(ITEM, TAKER, CHAIR, ROOM);
        var service = await NewServiceAsync();

        var result = await service.MoveAsync(GiveBack("rev-1"), Ct);

        result.Outcome.Should().Be(ReversalOutcome.NotWhereExpected);
        (_world.Row(ITEM).PlayerEntityId, _world.Row(ITEM).RoomEntityId).Should().Be((TAKER, ROOM));
        _world.Movements().Should().BeEmpty();
        Receipts().Should().BeEmpty("a refused reversal can be asked again");
    }

    [Fact]
    public async Task TheSameOperationAskedAgain_IsAlreadyApplied_AndMovesNothingMore()
    {
        _world.AddFurni(ITEM, TAKER, CHAIR, roomId: null);
        var service = await NewServiceAsync();

        (await service.MoveAsync(GiveBack("rev-1"), Ct))
            .Outcome.Should()
            .Be(ReversalOutcome.Applied);
        var again = await service.MoveAsync(GiveBack("rev-1"), Ct);

        again.Should().Be(new ReversalResult(ReversalOutcome.AlreadyApplied, ITEM));
        _world.Row(ITEM).PlayerEntityId.Should().Be(GIVER);
        _world.Movements().Should().ContainSingle();
        Receipts().Should().ContainSingle();
    }

    [Fact]
    public async Task AnUnsettledReceipt_WhoseFurniStandsAtTheTarget_IsAlreadyApplied()
    {
        // A call that moved the furni and crashed before settling its receipt.
        _world.AddFurni(ITEM, GIVER, CHAIR, roomId: null);
        _world.Db.Insert(
            new AssetReversalReceiptEntity
            {
                OperationId = "rev-1",
                ReversedOperationId = TRADE_OPERATION,
                Actor = STAFF,
                Reason = "duped trade",
                AssetKind = AssetKind.Furni,
                AssetId = ITEM,
                CreatedAt = DateTime.UtcNow,
            }
        );
        var service = await NewServiceAsync();

        var result = await service.MoveAsync(GiveBack("rev-1"), Ct);

        result.Outcome.Should().Be(ReversalOutcome.AlreadyApplied);
        _world.Movements().Should().BeEmpty();
        Receipts().Single().Outcome.Should().Be(ReversalOutcome.Applied);
    }

    [Fact]
    public async Task ABorrowedFurni_IsRefusedAsUnsupported_AndNothingMoves()
    {
        _world.AddFurni(ITEM, TAKER, CHAIR, roomId: null);
        var service = await NewServiceAsync();

        var result = await service.MoveAsync(
            GiveBack("rev-1") with
            {
                Asset = new AssetRef(AssetKind.BorrowedFurni, ITEM, CHAIR, null),
            },
            Ct
        );

        result.Outcome.Should().Be(ReversalOutcome.Unsupported);
        _world.Row(ITEM).PlayerEntityId.Should().Be(TAKER);
        _world.Movements().Should().BeEmpty();
        Receipts().Should().BeEmpty();
    }

    [Fact]
    public async Task AnActorWithoutTheNode_IsForbidden_AndNothingMoves()
    {
        _world.AddFurni(ITEM, TAKER, CHAIR, roomId: null);
        var service = await NewServiceAsync();

        var result = await service.MoveAsync(GiveBack("rev-1") with { ActorPlayerId = GIVER }, Ct);

        result.Outcome.Should().Be(ReversalOutcome.Forbidden);
        _world.Row(ITEM).PlayerEntityId.Should().Be(TAKER);
        _world.Movements().Should().BeEmpty();
        Receipts().Should().BeEmpty();
    }

    [Fact]
    public async Task ADupeTakenBack_IsDestroyedFromTheInventoryItIsIn()
    {
        _world.AddFurni(ITEM, TAKER, CHAIR, roomId: null);
        var service = await NewServiceAsync();

        var result = await service.MoveAsync(
            GiveBack("rev-1") with
            {
                Target = AssetPlace.Nowhere,
            },
            Ct
        );

        result.Outcome.Should().Be(ReversalOutcome.Applied);
        _world.Rows().Should().BeEmpty();
        var row = _world.Movements().Should().ContainSingle().Subject;
        (row.FromOwner, row.FromKind, row.ToKind)
            .Should()
            .Be((TAKER, PlaceKind.Inventory, PlaceKind.None));
        row.Cause.Should().Be(MovementCauses.REVERSAL);
    }

    [Fact]
    public async Task ADestroyedFurni_IsMadeAgainOnce_UnderANewId()
    {
        var service = await NewServiceAsync();
        var remake = GiveBack("rev-1") with
        {
            Expected = AssetPlace.Nowhere,
            Recreate = new AssetSnapshot(CHAIR, "{\"state\":\"1\"}"),
        };

        var made = await service.MoveAsync(remake, Ct);
        var again = await service.MoveAsync(remake, Ct);

        made.Outcome.Should().Be(ReversalOutcome.Applied);
        made.AssetId.Should().NotBe(0).And.NotBe(ITEM);
        again.Should().Be(new ReversalResult(ReversalOutcome.AlreadyApplied, made.AssetId));
        var row = _world.Rows().Should().ContainSingle().Subject;
        (row.Id, row.PlayerEntityId).Should().Be(((int)made.AssetId, GIVER));
        _world
            .Movements()
            .Should()
            .ContainSingle()
            .Which.Cause.Should()
            .Be(MovementCauses.REVERSAL);
    }

    [Fact]
    public async Task RecordingOff_TheFurniStillGoesBack_AndNoMovementIsWritten()
    {
        _world.Config.Set(new() { Enabled = false });
        _world.AddFurni(ITEM, TAKER, CHAIR, roomId: null);
        var service = await NewServiceAsync();

        var result = await service.MoveAsync(GiveBack("rev-1"), Ct);

        result.Outcome.Should().Be(ReversalOutcome.Applied);
        _world.Row(ITEM).PlayerEntityId.Should().Be(GIVER);
        _world.Movements().Should().BeEmpty();
    }

    // The chair a duped trade gave the taker, back to the giver.
    private static ReversalRequest GiveBack(string operationId) =>
        new()
        {
            OperationId = operationId,
            ReversedOperationId = TRADE_OPERATION,
            ActorPlayerId = STAFF,
            Reason = "duped trade",
            Asset = new AssetRef(AssetKind.Furni, ITEM, CHAIR, null),
            Expected = new AssetPlace(TAKER, PlaceKind.Inventory, 0, null),
            Target = new AssetPlace(GIVER, PlaceKind.Inventory, 0, null),
        };

    private async Task<AssetReversalService> NewServiceAsync()
    {
        foreach (var player in new[] { GIVER, TAKER })
            _world.Fakes.Instances[(typeof(IInventoryGrain), (long)player)] =
                await _world.NewInventoryAsync(player, Ct);

        return new AssetReversalService(
            _world.Fakes.Create<IGrainFactory>(),
            _world.Db,
            NullLogger<AssetReversalService>.Instance
        );
    }

    private List<AssetReversalReceiptEntity> Receipts()
    {
        using var db = _world.Db.CreateDbContext();

        return db.AssetReversalReceipts.AsNoTracking().ToList();
    }
}
