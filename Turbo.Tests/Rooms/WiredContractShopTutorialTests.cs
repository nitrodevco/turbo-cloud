using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The Wired Faculty tutorial "Automatic shop with contracts (easiest set-up possible)"
/// (18/03/2026): "User clicks furni" on the contracts, and Initiate Transaction offers "the
/// Triggering item (which means the contract the user clicked)". Its contracts slot took only
/// picked or selector furni, so the clicked contract was never offered.
/// </summary>
public sealed class WiredContractShopTutorialTests
{
    private const int PLAYER_INDEX = 5;
    private const int CONTRACT = 30;
    private const int BOX = 2;
    private const int CHEST = 20;

    private readonly WiredRoom _room = new(8, 8);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_clicked_contract_is_the_one_offered()
    {
        _room.Enter(PLAYER_INDEX, 1, 1);
        var chest = (FurnitureWiredChestLogic)
            _room
                .AddFloorItem(
                    CHEST,
                    6,
                    2,
                    "wired_chest_coins",
                    createLogic: (factory, ctx) => new FurnitureWiredCoinsChestLogic(factory, ctx)
                )
                .Logic;
        await chest.SetMapDataAsync(
            new Dictionary<string, string>
            {
                [WiredChestData.IS_WIRED_ENABLED] = WiredChestData.TRUE,
                [WiredChestData.LOCKED] = WiredChestData.FALSE,
            },
            refresh: false
        );
        _room.AddFloorItem(
            CONTRACT,
            4,
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
                stuffIds: [CHEST],
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
        ctx.Selected.SelectedFurniIds.Add(CONTRACT);

        await action.ExecuteAsync(ctx, Ct);

        _room
            .Harness.Fakes.Log.Of(nameof(IWiredTradeGrain.StartContractAsync))
            .Select(x => ((WiredContractTradeRequest)x.Args[0]!).SourceId.Value)
            .Should()
            .Equal(CONTRACT);
    }
}
