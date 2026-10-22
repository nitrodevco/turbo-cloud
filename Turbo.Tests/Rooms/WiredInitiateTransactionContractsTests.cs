using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Initiate Transaction offers exactly one contract. Habbo's Creator Tools name the failure for
/// anything else "Misconfig Too Many Or No Contracts" (failure reason 21); the box offered the
/// first of several contracts instead. A custom contract is the contract of the transactions
/// made with it, so the transaction triggers pick it like any other contract.
/// </summary>
public sealed class WiredInitiateTransactionContractsTests
{
    private const int PLAYER_INDEX = 5;
    private const int BOX = 2;
    private const int CUSTOM_CONTRACT = 3;
    private const int TRIGGER = 4;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(0, WiredTransactionFailureType.Misconfig)]
    [InlineData(1, WiredTransactionFailureType.NoOrLockedChests)]
    [InlineData(2, WiredTransactionFailureType.Misconfig)]
    public async Task Only_one_contract_gets_past_the_contract_check(
        int contracts,
        WiredTransactionFailureType failure
    )
    {
        _room.Enter(PLAYER_INDEX, 1, 1);

        for (var i = 0; i < contracts; i++)
            _room.AddFloorItem(
                30 + i,
                4 + i,
                4,
                "wired_contract_payment",
                createLogic: (factory, ctx) => new FurnitureWiredPaymentContractLogic(factory, ctx)
            );

        var action = _room.AddBox<WiredActionInitiateTransaction>(
            BOX,
            0,
            0,
            "wf_act_init_transaction"
        );
        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [0, 1, 0, 0, 0, 300],
                stuffIds2: [.. Enumerable.Range(30, contracts)],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.TriggeredItem],
                    [WiredFurniSourceType.SelectedItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Selected.SelectedAvatarIds.Add(PLAYER_INDEX);
        ctx.Selected.SelectedFurniIds.UnionWith(Enumerable.Range(30, contracts));

        await action.ExecuteAsync(ctx, Ct);

        _room
            .Harness.Fakes.Sent<WiredTransactionFailMessageComposer>()
            .Select(x => x.FailureType)
            .Should()
            .Equal(failure);
    }

    [Fact]
    public async Task A_custom_contract_transaction_is_the_custom_contracts_for_the_triggers()
    {
        _room.Enter(PLAYER_INDEX, 1, 1);
        var custom = _room.AddBox<WiredAddonCustomContract>(
            CUSTOM_CONTRACT,
            0,
            0,
            "wf_xtra_custom_contract"
        );
        var action = _room.AddBox<WiredActionInitiateTransaction>(
            BOX,
            0,
            0,
            "wf_act_init_transaction"
        );
        var trigger = _room.AddBox<WiredTriggerTransactionFailed>(
            TRIGGER,
            6,
            6,
            "wf_trg_transaction_fail"
        );
        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [0, 1, 0, 0, 0, 300],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.TriggeredItem],
                    [WiredFurniSourceType.SelectedItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        // Tester report 2026-10-22: picking the custom contract was refused.
        (await _room.SaveAsync<UpdateTriggerMessage>(TRIGGER, stuffIds: [CUSTOM_CONTRACT]))
            .Should()
            .BeTrue();
        var heard = new HeardEvents();
        _room.Harness.Module<RoomEventModule>().Register(heard);

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Selected.SelectedAvatarIds.Add(PLAYER_INDEX);
        ctx.Policy.CustomContract = custom;

        await action.ExecuteAsync(ctx, Ct);

        var failed = heard.Events.OfType<WiredTransactionFailedEvent>().Single();
        failed.SourceId.Should().Be((RoomObjectId)CUSTOM_CONTRACT);
        (await trigger.MatchesEventAsync(failed, Ct)).Should().BeTrue();
    }

    private sealed class HeardEvents : IRoomEventListener
    {
        public List<RoomEvent> Events { get; } = [];

        public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
        {
            Events.Add(evt);

            return Task.CompletedTask;
        }
    }
}
