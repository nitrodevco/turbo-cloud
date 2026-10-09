using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.WiredTrading.Enums;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Initiate Transaction offers exactly one contract. Habbo's Creator Tools name the failure for
/// anything else "Misconfig Too Many Or No Contracts" (failure reason 21); the box offered the
/// first of several contracts instead.
/// </summary>
public sealed class WiredInitiateTransactionContractsTests
{
    private const int PLAYER_INDEX = 5;
    private const int BOX = 2;

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
            .Harness.Fakes.Log.Calls.SelectMany(call => call.Args)
            .OfType<WiredTransactionFailMessageComposer>()
            .Select(x => x.FailureType)
            .Should()
            .Equal(failure);
    }
}
