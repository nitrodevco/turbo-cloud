using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Turbo.Primitives.Action;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Rooms.Grains;
using Turbo.Primitives.Trading.Enums;
using Turbo.Rooms.Grains;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

public sealed class AtomicFurnitureTradeTests
{
    private const int ROOM = 5;
    private const int FIRST = 8;
    private const int SECOND = 9;
    private const int FIRST_ITEM = 100;
    private const int SECOND_ITEM = 200;
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Failure_writing_the_second_owner_change_keeps_both_offers_and_movements_unchanged()
    {
        var fault = new SecondOwnershipWrite();
        using var world = new FurnitureWorld(fault);
        var prepared = await ConfirmingAsync(world);
        var trades = prepared.Trades;
        fault.Enabled = true;

        await trades.ConfirmAsync(ActionContext.CreateForPlayer(FIRST, ROOM), true, Ct);
        await trades.ConfirmAsync(ActionContext.CreateForPlayer(SECOND, ROOM), true, Ct);

        fault.Writes.Should().Be(2, "the database fault must actually interrupt the exchange");
        world
            .Rows()
            .Select(x => (x.Id, x.PlayerEntityId))
            .Should()
            .Equal((FIRST_ITEM, FIRST), (SECOND_ITEM, SECOND));
        world.Movements().Should().BeEmpty("no half of a failed exchange may commit");
    }

    [Fact]
    public async Task A_failed_recipient_call_after_ownership_changes_cannot_leave_a_one_sided_trade()
    {
        using var world = new FurnitureWorld();
        var prepared = await ConfirmingAsync(world);
        var trades = prepared.Trades;
        var recipient = new Fakes();
        recipient.Handlers[nameof(IInventoryGrain.ReceiveFurnitureAsync)] = _ =>
            Task.FromException(new InvalidOperationException("recipient unavailable"));
        recipient.Handlers[nameof(IInventoryGrain.ReconcileFurnitureTradesAsync)] = _ =>
            Task.FromException(new InvalidOperationException("recipient unavailable"));
        world.Fakes.Instances[(typeof(IInventoryGrain), (long)SECOND)] =
            recipient.Create<IInventoryGrain>();

        await trades.ConfirmAsync(ActionContext.CreateForPlayer(FIRST, ROOM), true, Ct);
        await trades.ConfirmAsync(ActionContext.CreateForPlayer(SECOND, ROOM), true, Ct);

        world.Rows().Select(x => x.PlayerEntityId).Should().Equal(SECOND, FIRST);
        recipient
            .Log.Of(nameof(IInventoryGrain.ReconcileFurnitureTradesAsync))
            .Should()
            .ContainSingle();
        await using var db = await world.Db.CreateDbContextAsync(Ct);
        var receipt = await db.FurnitureTradeReceipts.SingleAsync(Ct);
        receipt.InitiatorReconciled.Should().BeTrue();
        receipt.PartnerReconciled.Should().BeFalse("failed delivery remains durably retryable");
    }

    [Fact]
    public async Task Cancellation_between_owner_writes_rolls_back_and_can_retry_the_frozen_operation()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var fault = new SecondOwnershipWrite { Cancel = cancellation };
        using var world = new FurnitureWorld(fault);
        var prepared = await ConfirmingAsync(world);
        fault.Enabled = true;
        await prepared.Trades.ConfirmAsync(ActionContext.CreateForPlayer(FIRST, ROOM), true, Ct);

        (
            await prepared.Trades.ConfirmAsync(
                ActionContext.CreateForPlayer(SECOND, ROOM),
                true,
                cancellation.Token
            )
        )
            .Should()
            .BeFalse();

        world.Rows().Select(x => x.PlayerEntityId).Should().Equal(FIRST, SECOND);
        world.Movements().Should().BeEmpty();
        var operation = prepared.Session.CommitCause!.OperationId;
        await using (var db = await world.Db.CreateDbContextAsync(Ct))
            (await db.FurnitureTradeReceipts.AnyAsync(Ct)).Should().BeFalse();
        (await prepared.Trades.ConfirmAsync(ActionContext.CreateForPlayer(SECOND, ROOM), true, Ct))
            .Should()
            .BeTrue();
        prepared.Session.CommitCause.OperationId.Should().Be(operation);
        world.Rows().Select(x => x.PlayerEntityId).Should().Equal(SECOND, FIRST);
    }

    [Fact]
    public async Task A_lost_commit_reply_retries_the_same_frozen_operation_before_validating_old_ownership()
    {
        using var world = new FurnitureWorld();
        var prepared = await ConfirmingAsync(world);
        var endpoint = new Fakes();
        var attempts = 0;
        var operations = new List<string>();
        endpoint.Handlers[nameof(IInventoryGrain.CommitFurnitureTradeAsync)] = call =>
            CommitAsync();
        world.Fakes.Instances[(typeof(IInventoryGrain), (long)FIRST)] =
            endpoint.Create<IInventoryGrain>();

        async Task<bool> CommitAsync()
        {
            var request = prepared.Session.CommitRequest!;
            var cause = prepared.Session.CommitCause!;
            operations.Add(cause.OperationId);
            var result = await prepared.First.CommitFurnitureTradeAsync(request, cause, Ct);
            if (++attempts == 1)
                throw new TimeoutException("successful reply was lost");
            return result;
        }

        await prepared.Trades.ConfirmAsync(ActionContext.CreateForPlayer(FIRST, ROOM), true, Ct);
        (await prepared.Trades.ConfirmAsync(ActionContext.CreateForPlayer(SECOND, ROOM), true, Ct))
            .Should()
            .BeFalse();
        world.Rows().Select(x => x.PlayerEntityId).Should().Equal(SECOND, FIRST);
        prepared.Session.State.Should().Be(TradeStateType.Confirming);
        (await prepared.Trades.ConfirmAsync(ActionContext.CreateForPlayer(SECOND, ROOM), false, Ct))
            .Should()
            .BeFalse();
        (await prepared.Trades.ConfirmAsync(ActionContext.CreateForPlayer(SECOND, ROOM), true, Ct))
            .Should()
            .BeTrue();

        operations.Should().HaveCount(2).And.OnlyContain(x => x == operations[0]);
        world.Movements().Should().HaveCount(2, "replaying completion moves nothing twice");
        prepared.Session.State.Should().Be(TradeStateType.Completed);
        (await prepared.First.GetAllItemSnapshotsAsync(Ct))
            .Select(x => x.ItemId.Value)
            .Should()
            .Equal(SECOND_ITEM);
        (await prepared.Second.GetAllItemSnapshotsAsync(Ct))
            .Select(x => x.ItemId.Value)
            .Should()
            .Equal(FIRST_ITEM);
    }

    private sealed record Prepared(
        IRoomTradeGrain Trades,
        IInventoryGrain First,
        IInventoryGrain Second,
        TradeSession Session
    );

    private static async Task<Prepared> ConfirmingAsync(FurnitureWorld world)
    {
        world.AddPlayer(FIRST, "first");
        world.AddPlayer(SECOND, "second");
        world.AddDefinition(1, "chair", "default_floor");
        world.AddFurni(FIRST_ITEM, FIRST, 1, null);
        world.AddFurni(SECOND_ITEM, SECOND, 1, null);
        var first = await world.NewInventoryAsync(FIRST, Ct);
        var second = await world.NewInventoryAsync(SECOND, Ct);
        world.Fakes.Instances[(typeof(IInventoryGrain), (long)FIRST)] = first;
        world.Fakes.Instances[(typeof(IInventoryGrain), (long)SECOND)] = second;
        var session = new TradeSession
        {
            InitiatorId = FIRST,
            PartnerId = SECOND,
            State = TradeStateType.Confirming,
        };
        session.InitiatorSide.Items[FIRST_ITEM] = (
            await first.GetItemSnapshotAsync(FIRST_ITEM, Ct)
        )!;
        session.PartnerSide.Items[SECOND_ITEM] = (
            await second.GetItemSnapshotAsync(SECOND_ITEM, Ct)
        )!;
        var state = new RoomTradeLiveState { RoomId = ROOM };
        state.TradesByPlayerId[FIRST] = session;
        state.TradesByPlayerId[SECOND] = session;
        var grain = GrainHarness.Create<RoomTradeGrain>(world.Fakes, world.Db, ROOM);
        GrainHarness.Replace(grain, state);
        return new Prepared(grain, first, second, session);
    }

    private sealed class SecondOwnershipWrite : DbCommandInterceptor
    {
        public bool Enabled { get; set; }
        public int Writes { get; private set; }
        public CancellationTokenSource? Cancel { get; set; }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            if (
                Enabled
                && command.CommandText.StartsWith("UPDATE \"furniture\"", StringComparison.Ordinal)
                && ++Writes == 2
            )
            {
                if (Cancel is { } cancellation)
                {
                    await cancellation.CancelAsync();
                    cancellationToken.ThrowIfCancellationRequested();
                }
                throw new InvalidOperationException("second ownership write failed");
            }
            return result;
        }
    }
}
