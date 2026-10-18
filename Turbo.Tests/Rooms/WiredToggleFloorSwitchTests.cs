using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Habbo's Toggle Furni State effect does not flip a floor switch: pointed at Floor Switch 1 and a
/// Pura lamp, it toggled the lamp and left the switch at 0 and out of its ObjectsDataUpdate, 2 of
/// 2 (official client capture, 2026-10-18, build WIN63-202610151745).
/// </summary>
public sealed class WiredToggleFloorSwitchTests
{
    private const int CLICK_ME = 20;
    private const int SWITCH = 21;
    private const int LAMP = 22;
    private const int TRIGGER = 1;
    private const int EFFECT = 2;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Theory]
    [InlineData("wf_floor_switch1")]
    [InlineData("wf_floor_switch2")]
    public async Task Toggle_furni_state_leaves_a_floor_switch_as_it_is(string switchName)
    {
        await BuildAsync(switchName);

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);

        List<IComposer> sent = [];
        for (var i = 0; i < 10; i++)
            sent.AddRange(await TickAsync());

        _room.FloorItem(LAMP).Logic.GetState().Should().Be(1, "the lamp beside it is toggled");
        _room.FloorItem(SWITCH).Logic.GetState().Should().Be(0);
        sent.OfType<ObjectsDataUpdateMessageComposer>()
            .SelectMany(x => x.StuffDatas.Select(s => (int)s.Item1))
            .Should()
            .Contain(LAMP)
            .And.NotContain(SWITCH);
    }

    private async Task BuildAsync(string switchName)
    {
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(
            SWITCH,
            3,
            3,
            switchName,
            canWalk: false,
            createLogic: (_, ctx) =>
                _room.Harness.LogicProvider.CreateLogicInstance(switchName, ctx),
            totalStates: switchName == "wf_floor_switch2" ? 4 : 2
        );
        _room.AddFloorItem(LAMP, 4, 3, canWalk: false);
        _room.AddBox<WiredTriggerClickFurni>(TRIGGER, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionToggleItemState>(EFFECT, 0, 0, "wf_act_toggle_state");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                TRIGGER,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams: [0],
                stuffIds: [SWITCH, LAMP],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );
        await TickAsync();
    }

    private async Task<List<IComposer>> TickAsync()
    {
        var before = _room.Harness.Fakes.Log.Calls.Count;
        _now += 50;
        await Wired.ProcessWiredAsync(_now, dormant: false, Ct);

        return _room
            .Harness.Fakes.Log.Calls.Skip(before)
            .SelectMany(x => x.Args)
            .OfType<RoomOutboundSnapshot>()
            .SelectMany(x => x.Composers)
            .ToList();
    }
}
