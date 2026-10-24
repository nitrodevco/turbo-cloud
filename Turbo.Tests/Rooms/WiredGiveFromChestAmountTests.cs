using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.WiredTrading;
using Turbo.Primitives.WiredTrading.Grains;
using Turbo.Primitives.WiredTrading.Snapshots;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.WiredTrading;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Give Currency From Chest hands out the amount it was set to: a number, or a variable's value
/// (wired matrix action.45.E10/E11, the editor's ValueOrVariableSection, AS3 WIN63-202610201032
/// wired_setup/actiontypes/chests/_-U1N.as:110-113). "Everything" is its own mode (E9, mode 1);
/// a variable that gives no value is no amount, and the chest is left as it is.
/// </summary>
public sealed class WiredGiveFromChestAmountTests
{
    private const int PLAYER_INDEX = 5;
    private const int BOX = 2;
    private const int CHEST = 30;
    private const int CHEST_COINS = 500;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WiredRoom _room = new(8, 8);
    private readonly List<int?> _withdrawn = [];

    [Fact]
    public async Task A_number_amount_takes_that_many_credits_from_the_chest()
    {
        var (box, ctx) = await SetUpAsync([0, 7, 0, -20, 1, 11]);

        (await box.ExecuteAsync(ctx, Ct)).Should().BeTrue();

        _withdrawn.Should().Equal(7);
        _room.Harness.Fakes.Sent<WiredTransactionSuccessMessageComposer>().Should().ContainSingle();
    }

    [Fact]
    public async Task The_everything_mode_takes_all_the_chest_holds()
    {
        var (box, ctx) = await SetUpAsync([1, 1, 0, -20, 1, 11]);

        (await box.ExecuteAsync(ctx, Ct)).Should().BeTrue();

        _withdrawn.Should().Equal(CHEST_COINS);
    }

    [Fact]
    public async Task A_variable_amount_with_no_value_gives_nothing_rather_than_everything()
    {
        // Variable mode with no variable picked ("n"): there is no value to read.
        var (box, ctx) = await SetUpAsync([0, 1, 1, -20, 1, 11]);

        (await box.ExecuteAsync(ctx, Ct)).Should().BeFalse();

        _withdrawn.Should().BeEmpty();
        _room.Harness.Fakes.Sent<WiredTransactionSuccessMessageComposer>().Should().BeEmpty();
    }

    private async Task<(WiredActionGiveCurrencyFromChest, WiredExecutionContext)> SetUpAsync(
        int[] ints
    )
    {
        _room.Enter(PLAYER_INDEX, 1, 1);

        var chest = (FurnitureWiredChestLogic)
            _room
                .AddFloorItem(
                    CHEST,
                    4,
                    4,
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
        typeof(FurnitureWiredChestLogic)
            .GetProperty(nameof(FurnitureWiredChestLogic.Summary))!
            .SetValue(chest, WiredChestSummarySnapshot.Empty with { Coins = CHEST_COINS });
        _room.Harness.Fakes.Handlers[nameof(IWiredChestGrain.WithdrawAsync)] = call =>
        {
            var amount = ((WiredChestWithdrawRequest)call.Args[0]!).Amount;

            _withdrawn.Add(amount);

            return Task.FromResult(
                new WiredChestMoveResultSnapshot
                {
                    Failure = null,
                    TransactionId = 1,
                    Coins = amount ?? CHEST_COINS,
                    Items = [],
                    Summary = WiredChestSummarySnapshot.Empty,
                }
            );
        };

        var box = _room.AddBox<WiredActionGiveCurrencyFromChest>(BOX, 0, 0, "wf_act_give_currency");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: ints,
                stuffIds: [CHEST],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.SelectorItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                    [WiredPlayerSourceType.SelectorUsers],
                ],
                definitionSpecifics: [0],
                variableIds: ["n"]
            )
        ).Should().BeTrue();

        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Selected.SelectedAvatarIds.Add(PLAYER_INDEX);

        return (box, ctx);
    }
}
