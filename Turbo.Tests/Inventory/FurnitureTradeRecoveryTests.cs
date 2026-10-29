using System.Collections.Immutable;
using System.Data.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Furniture.Custody;
using Turbo.Inventory;
using Turbo.Inventory.Configuration;
using Turbo.Inventory.Grains.Unseen;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Inventory.Grains;
using Turbo.Primitives.Movements;
using Turbo.Primitives.Movements.Enums;
using Turbo.Primitives.Trading;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Inventory;

public sealed class FurnitureTradeRecoveryTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_cached_inventory_discovers_pending_delivery_before_serving_its_old_list()
    {
        using var setup = await Setup.CreateAsync();
        (await setup.Second.GetAllItemSnapshotsAsync(Ct)).Single().ItemId.Value.Should().Be(200);
        await setup.CommitAsync();

        (await setup.Second.GetAllItemSnapshotsAsync(Ct)).Single().ItemId.Value.Should().Be(100);
        (await setup.First.GetAllItemSnapshotsAsync(Ct)).Single().ItemId.Value.Should().Be(200);
        await using var db = await setup.World.Db.CreateDbContextAsync(Ct);
        var receipt = await db.FurnitureTradeReceipts.SingleAsync(Ct);
        receipt.InitiatorReconciled.Should().BeTrue();
        receipt.PartnerReconciled.Should().BeTrue();
        setup.World.Movements().Select(x => x.Seq).Should().Equal((short)0, (short)1);
    }

    [Fact]
    public async Task Recovery_survives_a_failed_recipient_and_a_new_inventory_activation()
    {
        using var setup = await Setup.CreateAsync();
        await setup.CommitAsync();
        var unavailable = new Fakes();
        unavailable.Handlers[nameof(IInventoryGrain.ReconcileFurnitureTradesAsync)] = _ =>
            Task.FromException(new InvalidOperationException("activation unavailable"));
        setup.World.Fakes.Instances[(typeof(IInventoryGrain), 9L)] =
            unavailable.Create<IInventoryGrain>();
        await setup.Recovery().RecoverAsync(Ct);
        await using (var db = await setup.World.Db.CreateDbContextAsync(Ct))
        {
            var receipt = await db.FurnitureTradeReceipts.SingleAsync(Ct);
            receipt.InitiatorReconciled.Should().BeTrue();
            receipt.PartnerReconciled.Should().BeFalse();
        }

        var activatedAgain = await setup.World.NewInventoryAsync(9, Ct);
        setup.World.Fakes.Instances[(typeof(IInventoryGrain), 9L)] = activatedAgain;
        // A new recovery instance carries no in-memory queue from the failed attempt.
        await setup.Recovery().RecoverAsync(Ct);
        (await activatedAgain.GetAllItemSnapshotsAsync(Ct)).Single().ItemId.Value.Should().Be(100);
        await using var read = await setup.World.Db.CreateDbContextAsync(Ct);
        (await read.FurnitureTradeReceipts.SingleAsync(Ct)).PartnerReconciled.Should().BeTrue();
        setup.World.Movements().Should().HaveCount(2);
    }

    [Fact]
    public async Task A_delayed_historical_receive_cannot_repopulate_a_reconciled_inventory()
    {
        using var setup = await Setup.CreateAsync();
        var oldSnapshot = setup.Request.PartnerItems.Single();
        await setup.CommitAsync();
        await setup.Recovery().RecoverAsync(Ct);

        await setup.Second.ReceiveFurnitureAsync([oldSnapshot], Ct);

        (await setup.Second.GetAllItemSnapshotsAsync(Ct))
            .Select(x => x.ItemId.Value)
            .Should()
            .Equal(100);
        (await setup.Second.GetItemSnapshotAsync(200, Ct)).Should().BeNull();
    }

    [Fact]
    public async Task A_receive_records_new_items_before_an_offline_inventory_activation_can_disappear()
    {
        using var setup = await Setup.CreateAsync();
        var unseen = GrainHarness.Create<PlayerUnseenItemsGrain>(
            setup.World.Fakes,
            setup.World.Db,
            9
        );
        await unseen.OnActivateAsync(Ct);
        Task? recorded = null;
        setup.World.Fakes.Handlers[nameof(IPlayerUnseenItemsGrain.AddAsync)] = call =>
        {
            if (call.Interface != typeof(IPlayerUnseenItemsGrain))
                return Fakes.NotHandled;
            return recorded = unseen.AddAsync(
                (UnseenItemCategory)call.Args[0]!,
                (ImmutableArray<int>)call.Args[1]!,
                Ct
            );
        };

        await setup.Second.ReceiveFurnitureAsync(setup.Request.PartnerItems, Ct);
        recorded
            .Should()
            .NotBeNull("receipt must not defer unseen persistence until a furniture read");
        await recorded!;
        // No inventory read or deactivation flush intervenes. New activations read the persisted hint.
        var activatedAgain = GrainHarness.Create<PlayerUnseenItemsGrain>(
            setup.World.Fakes,
            setup.World.Db,
            9
        );
        await activatedAgain.OnActivateAsync(Ct);
        (await activatedAgain.GetUnseenItemsAsync(Ct))[UnseenItemCategory.OwnedFurni]
            .Should()
            .Contain(200);
    }

    [Fact]
    public async Task A_failed_unseen_notification_keeps_trade_reconciliation_pending_for_retry()
    {
        using var setup = await Setup.CreateAsync();
        await setup.CommitAsync();
        setup.World.Fakes.Handlers[nameof(IPlayerUnseenItemsGrain.AddAsync)] = _ =>
            Task.FromException(new InvalidOperationException("unseen activation unavailable"));

        var repair = () => setup.First.ReconcileFurnitureTradesAsync(Ct);
        await repair.Should().ThrowAsync<InvalidOperationException>();
        await using (var db = await setup.World.Db.CreateDbContextAsync(Ct))
            (await db.FurnitureTradeReceipts.SingleAsync(Ct))
                .InitiatorReconciled.Should()
                .BeFalse();
        setup.World.Fakes.Handlers.TryRemove(nameof(IPlayerUnseenItemsGrain.AddAsync), out _);
        await setup.First.ReconcileFurnitureTradesAsync(Ct);
        await using var read = await setup.World.Db.CreateDbContextAsync(Ct);
        (await read.FurnitureTradeReceipts.SingleAsync(Ct)).InitiatorReconciled.Should().BeTrue();
    }

    [Fact]
    public async Task A_new_trade_committed_after_the_reload_is_not_acknowledged_by_the_older_batch()
    {
        var hook = new Interleave();
        using var setup = await Setup.CreateAsync(hook);
        await setup.CommitAsync();
        var secondCause = MovementCause.New(MovementCauses.TRADE, 8, 5);
        var reverse = new FurnitureTradeRequest
        {
            InitiatorId = 9,
            PartnerId = 8,
            InitiatorItems = [setup.Request.InitiatorItems.Single() with { OwnerId = 9 }],
            PartnerItems = [setup.Request.PartnerItems.Single() with { OwnerId = 8 }],
        };
        hook.BeforeReceiptAck = async () =>
            (await TestCustody.Over(setup.World.Db).ExchangeTradeAsync(reverse, secondCause, Ct))
                .Should()
                .BeTrue();

        await setup.First.ReconcileFurnitureTradesAsync(Ct);

        await using (var db = await setup.World.Db.CreateDbContextAsync(Ct))
        {
            var next = await db.FurnitureTradeReceipts.SingleAsync(
                x => x.OperationId == secondCause.OperationId,
                Ct
            );
            next.PartnerReconciled.Should().BeFalse("the prior reload predates this exchange");
        }
        (await setup.First.GetAllItemSnapshotsAsync(Ct))
            .Select(x => x.ItemId.Value)
            .Should()
            .Equal(100);
    }

    [Fact]
    public async Task A_receive_during_a_reload_leaves_its_trade_obligation_pending()
    {
        var hook = new Interleave();
        using var setup = await Setup.CreateAsync(hook);
        await setup.CommitAsync();
        hook.BeforeFurnitureRead = () =>
            setup.First.ReceiveFurnitureAsync(setup.Request.PartnerItems, Ct);

        await setup.First.ReconcileFurnitureTradesAsync(Ct);

        await using (var db = await setup.World.Db.CreateDbContextAsync(Ct))
            (await db.FurnitureTradeReceipts.SingleAsync(Ct))
                .InitiatorReconciled.Should()
                .BeFalse();
        await setup.First.ReconcileFurnitureTradesAsync(Ct);
        await using var read = await setup.World.Db.CreateDbContextAsync(Ct);
        (await read.FurnitureTradeReceipts.SingleAsync(Ct)).InitiatorReconciled.Should().BeTrue();
    }

    [Fact]
    public async Task An_unavailable_old_receipt_does_not_starve_later_trades_in_a_bounded_recovery_pass()
    {
        using var setup = await Setup.CreateAsync();
        await setup.CommitAsync();
        var reverse = new FurnitureTradeRequest
        {
            InitiatorId = 9,
            PartnerId = 8,
            InitiatorItems = [setup.Request.InitiatorItems.Single() with { OwnerId = 9 }],
            PartnerItems = [setup.Request.PartnerItems.Single() with { OwnerId = 8 }],
        };
        var later = MovementCause.New(MovementCauses.TRADE, 8, 5);
        (await TestCustody.Over(setup.World.Db).ExchangeTradeAsync(reverse, later, Ct))
            .Should()
            .BeTrue();
        var unavailable = new Fakes();
        unavailable.Handlers[nameof(IInventoryGrain.ReconcileFurnitureTradesAsync)] = _ =>
            Task.FromException(new InvalidOperationException("unavailable"));
        var recovery = setup.Recovery(
            new InventoryConfig { TradeRecoveryBatchSize = 1 },
            unavailable.Create<IGrainFactory>()
        );

        await recovery.RecoverAsync(Ct);
        await recovery.RecoverAsync(Ct);

        await using var db = await setup.World.Db.CreateDbContextAsync(Ct);
        (await db.FurnitureTradeReceipts.ToListAsync(Ct))
            .Should()
            .OnlyContain(x => x.ReconcileAttemptedAt > DateTime.UnixEpoch);
        unavailable
            .Log.Of(nameof(IInventoryGrain.ReconcileFurnitureTradesAsync))
            .Should()
            .HaveCount(4);
    }

    private sealed class Setup(
        FurnitureWorld world,
        IInventoryGrain first,
        IInventoryGrain second,
        FurnitureTradeRequest request
    ) : IDisposable
    {
        public FurnitureWorld World { get; } = world;
        public IInventoryGrain First { get; } = first;
        public IInventoryGrain Second { get; } = second;
        public FurnitureTradeRequest Request { get; } = request;
        public MovementCause Cause { get; } = MovementCause.New(MovementCauses.TRADE, 9, 5);

        public void Dispose() => World.Dispose();

        public async Task CommitAsync() =>
            (await First.CommitFurnitureTradeAsync(Request, Cause, Ct)).Should().BeTrue();

        public FurnitureTradeRecovery Recovery(
            InventoryConfig? config = null,
            IGrainFactory? grains = null
        ) =>
            new(
                World.Db,
                grains ?? World.Fakes.Create<IGrainFactory>(),
                Options.Create(config ?? new InventoryConfig()),
                TimeProvider.System,
                NullLogger<FurnitureTradeRecovery>.Instance
            );

        public static async Task<Setup> CreateAsync(params IInterceptor[] interceptors)
        {
            var world = new FurnitureWorld(interceptors);
            world.AddPlayer(8, "first");
            world.AddPlayer(9, "second");
            world.AddDefinition(1, "chair", "default_floor");
            world.AddFurni(100, 8, 1, null);
            world.AddFurni(200, 9, 1, null);
            var first = await world.NewInventoryAsync(8, Ct);
            var second = await world.NewInventoryAsync(9, Ct);
            world.Fakes.Instances[(typeof(IInventoryGrain), 8L)] = first;
            world.Fakes.Instances[(typeof(IInventoryGrain), 9L)] = second;
            return new Setup(
                world,
                first,
                second,
                new FurnitureTradeRequest
                {
                    InitiatorId = 8,
                    PartnerId = 9,
                    InitiatorItems = [(await first.GetItemSnapshotAsync(100, Ct))!],
                    PartnerItems = [(await second.GetItemSnapshotAsync(200, Ct))!],
                }
            );
        }
    }

    private sealed class Interleave : DbCommandInterceptor
    {
        public Func<Task>? BeforeReceiptAck { get; set; }
        public Func<Task>? BeforeFurnitureRead { get; set; }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default
        )
        {
            if (
                command.CommandText.StartsWith(
                    "UPDATE \"furniture_trade_receipts\"",
                    StringComparison.Ordinal
                ) && BeforeReceiptAck is { } callback
            )
            {
                BeforeReceiptAck = null;
                await callback();
            }
            return result;
        }

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default
        )
        {
            if (
                command.CommandText.Contains("FROM \"furniture\"", StringComparison.Ordinal)
                && BeforeFurnitureRead is { } callback
            )
            {
                BeforeFurnitureRead = null;
                await callback();
            }
            return result;
        }
    }
}
