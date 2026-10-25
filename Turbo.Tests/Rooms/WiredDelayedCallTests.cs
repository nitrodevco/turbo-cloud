using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A delayed Call Another Stack is resolved when its stack fires; a delayed Send Signal when it
/// arrives (official client capture, WIN63-202610201032, wired matrix W4). Call 1 s into an effect
/// of 2 s: the effect ran at +3 s, but its box flashed with the caller at +0.09 s
/// (action.18.X3, 2026-10-24T23-04-05, 2/2). The called stack's Users In Area picked the user
/// standing there at the click although they had left before the call's 2 s were over
/// (action.18.X3b, 2026-10-25T01-40-54, 2/2); the receiver of a signal sent the same way picked
/// nobody (action.30.X3b, same recording, 2/2).
/// </summary>
public sealed class WiredDelayedCallTests
{
    private const int CLICK_ME = 30;
    private const int ANTENNA = 31;
    private const int OTHER = 32;
    private const int USER = 5;
    private const int PLAYER = 105;

    // Caller at (0,0), called stack or receiver at (2,0).
    private const int CALLER_TRIGGER = 1;
    private const int CALLER_ACTION = 2;
    private const int TARGET_TRIGGER = 3;
    private const int TARGET_SELECTOR = 4;
    private const int TARGET_ACTION = 5;

    private readonly WiredRoom _room = new(10, 10);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    private long Now => _room.Harness.NowMs();

    public WiredDelayedCallTests()
    {
        _room.Enter(USER, 5, 5);
        _room.AddFloorItem(CLICK_ME, 9, 9);
        _room.AddFloorItem(OTHER, 9, 8);
        _room.AddFloorItem(ANTENNA, 8, 9, "wf_antenna1");
    }

    // action.18.X3: "T" at click +3.09 / +3.17 s; the T box in the caller's ObjectsDataUpdate at
    // click +0.09 s and in no later one.
    [Fact]
    public async Task Action18X3_ADelayedCallFlashesTheCalledEffectAtOnceAndTheDelaysAdd()
    {
        await TargetStackAsync(receivesSignal: false, selectsArea: false);
        _room.AddBox<WiredActionWriteToLogs>(TARGET_ACTION, 2, 0, "wf_act_log");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                TARGET_ACTION,
                intParams: [(int)WiredLogLevelType.Warning],
                stringParam: "T",
                definitionSpecifics: [4]
            )
        )
            .Should()
            .BeTrue();
        await CallerStackAsync(signal: false, delayPulses: 2);
        await StartAsync();

        var clickedAt = await ClickAsync();
        await StepAsync();

        Flashes(TARGET_ACTION).Should().Be(1, "the called effect flashes with the caller");

        var ran = await RunUntilLoggedAsync("T", 4_000);

        ran.Should().BeInRange(clickedAt + 3_000, clickedAt + 3_100, "1 s of call + 2 s of effect");
        Flashes(TARGET_ACTION).Should().Be(1);
    }

    // action.18.X3b / action.30.X3b: Hutch. on the area's tile at the click, off it at +1.8 s,
    // the call / signal due at +2 s.
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task Action18X3b_ADelayedCallSelectsAtTheClickAndADelayedSignalOnArrival(
        bool signal,
        int whispers
    )
    {
        await TargetStackAsync(receivesSignal: signal, selectsArea: true);
        _room.AddBox<WiredActionShowMessage>(TARGET_ACTION, 2, 0, "wf_act_show_message");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                TARGET_ACTION,
                intParams: [0, 34, -1],
                stringParam: "CALL",
                playerSources:
                [
                    [WiredPlayerSourceType.SelectorUsers],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        await CallerStackAsync(signal, delayPulses: 4);
        await StartAsync();

        await ClickAsync();
        await StepAsync();
        _room.Avatars[USER].SetPosition(7, 7);

        for (var i = 0; i < 60; i++)
            await StepAsync();

        _room
            .Harness.Fakes.SentToPlayer<ChatMessageComposer>(PLAYER)
            .Count(x => x.Text == "CALL")
            .Should()
            .Be(whispers);
    }

    /// <summary>A stack nobody fires itself: a click on furni nobody clicks, or a receiver.</summary>
    private async Task TargetStackAsync(bool receivesSignal, bool selectsArea)
    {
        if (receivesSignal)
        {
            _room.AddBox<WiredTriggerReceiveSignal>(TARGET_TRIGGER, 2, 0, "wf_trg_recv_signal");
            (
                await _room.SaveAsync<UpdateTriggerMessage>(
                    TARGET_TRIGGER,
                    stuffIds: [ANTENNA],
                    furniSources:
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ]
                )
            ).Should().BeTrue();
        }
        else
        {
            _room.AddBox<WiredTriggerClickFurni>(TARGET_TRIGGER, 2, 0, "wf_trg_click_furni");
            (
                await _room.SaveAsync<UpdateTriggerMessage>(
                    TARGET_TRIGGER,
                    stuffIds: [OTHER],
                    furniSources:
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ]
                )
            ).Should().BeTrue();
        }

        if (!selectsArea)
            return;

        // The 3x3 around the user.
        _room.AddBox<WiredSelectorEntitiesInArea>(TARGET_SELECTOR, 2, 0, "wf_slc_users_area");
        (await _room.SaveAsync<UpdateSelectorMessage>(TARGET_SELECTOR, intParams: [4, 4, 3, 3]))
            .Should()
            .BeTrue();
    }

    private async Task CallerStackAsync(bool signal, int delayPulses)
    {
        _room.AddBox<WiredTriggerClickFurni>(CALLER_TRIGGER, 0, 0, "wf_trg_click_furni");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                CALLER_TRIGGER,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        if (signal)
        {
            _room.AddBox<WiredActionSendSignal>(CALLER_ACTION, 0, 0, "wf_act_send_signal");
            (
                await _room.SaveAsync<UpdateActionMessage>(
                    CALLER_ACTION,
                    intParams: [0, 0],
                    stuffIds: [ANTENNA],
                    furniSources:
                    [
                        [WiredFurniSourceType.SelectedItems],
                        [WiredFurniSourceType.SecondaryItems],
                    ],
                    playerSources:
                    [
                        [WiredPlayerSourceType.TriggeredUser],
                    ],
                    definitionSpecifics: [delayPulses]
                )
            ).Should().BeTrue();

            return;
        }

        _room.AddBox<WiredActionCallStacks>(CALLER_ACTION, 0, 0, "wf_act_call_stacks");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                CALLER_ACTION,
                stuffIds: [TARGET_TRIGGER],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [delayPulses]
            )
        ).Should().BeTrue();
    }

    private async Task StartAsync()
    {
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0), _room.Map.ToIdx(2, 0)],
            },
            Ct
        );
        await StepAsync();
    }

    private async Task<long> ClickAsync()
    {
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)PLAYER, (RoomId)1), 0, Ct);

        return Now;
    }

    /// <summary>One 50 ms wired tick, the room's clock moved with it.</summary>
    private async Task StepAsync()
    {
        _room.Harness.Clock.AdvanceMs(50);
        await Wired.ProcessWiredAsync(Now, dormant: false, Ct);
    }

    private async Task<long> RunUntilLoggedAsync(string text, long ms)
    {
        var end = Now + ms;

        while (Now < end)
        {
            await StepAsync();

            if (Wired.GetRoomLogPage(1, 10_000, -1, -1, text).TotalEntries > 0)
                return Now;
        }

        return -1;
    }

    private int Flashes(int boxId) =>
        _room
            .Harness.Fakes.SentToRoom<ObjectsDataUpdateMessageComposer>()
            .Count(update => update.StuffDatas.Any(entry => (int)entry.Item1 == boxId));
}
