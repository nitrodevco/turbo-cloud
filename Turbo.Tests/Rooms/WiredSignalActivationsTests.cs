using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// How many times a signal starts the receiving stack, as measured on the official server
/// (confidential capture, signal experiment): two forwarded furni travel in one signal and start
/// one activation, "for each furni" starts one per furni; the negative send fires only when the
/// sender's conditions fail. A click sends; the receiver writes one log line per activation.
/// </summary>
public sealed class WiredSignalActivationsTests
{
    private const int CLICK_ME = 20;
    private const int ANTENNA = 21;
    private const int FIRST = 22;
    private const int SECOND = 23;

    private const int SENDER_TRIGGER = 1;
    private const int SENDER_CONDITION = 2;
    private const int SENDER_ACTION = 3;
    private const int RECEIVER_TRIGGER = 4;
    private const int RECEIVER_ACTION = 5;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredSignalActivationsTests()
    {
        _room.Enter(5, 1, 1);
        _room.AddFloorItem(CLICK_ME, 7, 7);
        _room.AddFloorItem(ANTENNA, 6, 7, "wf_antenna1");
        _room.AddFloorItem(FIRST, 4, 5);
        _room.AddFloorItem(SECOND, 6, 5);
    }

    // action.30: signal_payload_batch_moves_two_once / signal_split_furniture_two_activations.
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task Action30_TwoForwardedFurni_StartOneActivation_OrOneEachWhenSplit(
        bool splitFurni,
        int activations
    )
    {
        await BuildAsync(negative: false, conditionPasses: true, splitFurni: splitFurni);

        await ClickAsync();

        Logged().Should().Be(activations);
    }

    // action.30 negative variant: negative_send_not_on_success / negative_send_on_condition_failure.
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public async Task Action30_TheNegativeSend_FiresOnlyWhenTheSendersConditionsFail(
        bool conditionPasses,
        int activations
    )
    {
        await BuildAsync(negative: true, conditionPasses: conditionPasses, splitFurni: false);

        await ClickAsync();

        Logged().Should().Be(activations);
    }

    private async Task BuildAsync(bool negative, bool conditionPasses, bool splitFurni)
    {
        _room.AddBox<WiredTriggerReceiveSignal>(RECEIVER_TRIGGER, 2, 0, "wf_trg_recv_signal");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                RECEIVER_TRIGGER,
                stuffIds: [ANTENNA],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        _room.AddBox<WiredActionWriteToLogs>(RECEIVER_ACTION, 2, 0, "wf_act_log");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                RECEIVER_ACTION,
                intParams: [(int)WiredLogLevelType.Warning],
                stringParam: "received",
                definitionSpecifics: [0]
            )
        )
            .Should()
            .BeTrue();

        _room.AddBox<WiredTriggerClickFurni>(SENDER_TRIGGER, 0, 0, "wf_trg_click_furni");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                SENDER_TRIGGER,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        // No selector picks furni: "exactly 0" passes, "exactly 5" fails.
        _room.AddBox<WiredConditionSelectQuantity>(SENDER_CONDITION, 0, 0, "wf_cnd_slc_quantity");
        (
            await _room.SaveAsync<UpdateConditionMessage>(
                SENDER_CONDITION,
                intParams: [0, conditionPasses ? 0 : 5, 1],
                furniSources:
                [
                    [WiredFurniSourceType.SelectorItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.SelectorUsers],
                ]
            )
        ).Should().BeTrue();

        if (negative)
            _room.AddBox<WiredActionNegativeSendSignal>(
                SENDER_ACTION,
                0,
                0,
                "wf_act_neg_send_signal"
            );
        else
            _room.AddBox<WiredActionSendSignal>(SENDER_ACTION, 0, 0, "wf_act_send_signal");

        (
            await _room.SaveAsync<UpdateActionMessage>(
                SENDER_ACTION,
                intParams: [splitFurni ? 1 : 0, 0],
                stuffIds: [ANTENNA],
                stuffIds2: [FIRST, SECOND],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                    [WiredFurniSourceType.SecondaryItems],
                ],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0), _room.Map.ToIdx(2, 0)],
            },
            Ct
        );
        await TickAsync(1);
    }

    private async Task ClickAsync()
    {
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(6);
    }

    private int Logged() => Wired.GetRoomLogPage(1, 10_000, -1, -1, "received").TotalEntries;

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
