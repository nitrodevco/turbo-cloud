using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The Wired Disabler (<c>conf_wired_disable</c>), plain furniture before (no logic), as Habbo
/// runs it (official client capture, 2026-10-18): states 0 and 1; while on, no trigger fires
/// (0 of 3) yet boxes still save; switching it tells the room its state only, no
/// ConfigurationItemStates; picked up while on, wired fires again at once (2 of 2).
/// </summary>
public sealed class WiredDisableTests
{
    private const int CLICK_ME = 20;
    private const int PAD = 21;
    private const int TRIGGER = 1;
    private const int EFFECT = 3;
    private const int DISABLER = 30;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ActionContext Owner => ActionContext.CreateForSystem(1);

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task A_switched_on_disabler_stops_triggers_firing_until_it_is_switched_off()
    {
        var pad = await BuildStackAsync();
        var disabler = await AddDisablerAsync();

        await ClickAsync();
        pad.Logic.GetState().Should().Be(1, "wired fires with the disabler off");

        await disabler.Logic.OnUseAsync(Owner, 0, Ct);
        disabler.Logic.GetState().Should().Be(1);

        for (var i = 0; i < 3; i++)
            await ClickAsync();

        pad.Logic.GetState().Should().Be(1, "no trigger fires while the disabler is on");

        await disabler.Logic.OnUseAsync(Owner, 0, Ct);
        disabler.Logic.GetState().Should().Be(0, "it switches between two states");

        await ClickAsync();
        pad.Logic.GetState().Should().Be(0, "wired fires again");
        _room.Harness.Fakes.SentToRoom<ConfigurationItemStatesMessageComposer>().Should().BeEmpty();
    }

    [Fact]
    public async Task Boxes_still_save_while_it_is_on_and_picking_it_up_while_on_lets_wired_fire_at_once()
    {
        var pad = await BuildStackAsync();
        var disabler = await AddDisablerAsync();
        await disabler.Logic.OnUseAsync(Owner, 0, Ct);

        (await SaveTriggerAsync()).Should().BeTrue("saving is not blocked");
        await ClickAsync();
        pad.Logic.GetState().Should().Be(0);

        _room.Harness.State.ItemsById.Remove((RoomObjectId)DISABLER);
        await disabler.Logic.OnDetachAsync(Ct);

        await ClickAsync();
        pad.Logic.GetState().Should().Be(1, "the switched-on disabler left the room");
    }

    [Fact]
    public async Task One_picked_up_while_on_is_placed_again_off_and_wired_fires()
    {
        var pad = await BuildStackAsync();
        var disabler = await AddDisablerAsync();
        await disabler.Logic.OnUseAsync(Owner, 0, Ct);
        await _room.Harness.Module<RoomObjectModule>().RemoveObjectAsync(Owner, disabler, Ct, 1);
        var addsBefore = _room.Harness.Fakes.SentToRoom<ObjectAddMessageComposer>().Count();

        (
            await _room
                .Harness.Module<RoomFurniModule>()
                .PlaceFloorItemAsync(Owner, disabler, 6, 6, Rotation.North, Ct)
        )
            .Should()
            .BeTrue();

        disabler.Logic.GetState().Should().Be(0);
        (
            (LegacyStuffSnapshot)
                _room
                    .Harness.Fakes.SentToRoom<ObjectAddMessageComposer>()
                    .Skip(addsBefore)
                    .Single()
                    .FloorItem.StuffData
        )
            .Data.Should()
            .Be("0", "the room is told of it off");
        await ClickAsync();
        pad.Logic.GetState().Should().Be(1, "wired fires with the re-placed disabler");
    }

    [Fact]
    public async Task Switched_off_a_one_second_timer_starts_over_two_avatar_ticks_on()
    {
        // Official client capture 2026-10-18: a 1 s timer firing on one avatar tick, switched off
        // 139 ms after the next tick, fired again on the second tick after (+866 and +848 ms),
        // not where its old schedule was (+361); then every second from there.
        var pad = await BuildTimerStackAsync();
        var disabler = await AddDisablerAsync();
        var fired = new List<long>();

        await RunUntilAsync(13_550, pad, fired);
        fired.Should().Equal(11_500, 12_500, 13_500);

        await disabler.Logic.OnUseAsync(Owner, 0, Ct);
        await RunUntilAsync(17_100, pad, fired);
        fired.Should().HaveCount(3, "the timer is still while the disabler is on");

        await disabler.Logic.OnUseAsync(Owner, 0, Ct);
        await RunUntilAsync(20_000, pad, fired);

        fired.Skip(3).Should().Equal(18_000, 19_000, 20_000);
    }

    [Fact]
    public async Task Picked_up_while_on_a_timer_keeps_its_schedule()
    {
        // Official client capture 2026-10-18: picked up while on, the timer fired 240 ms later, on
        // the phase it had before the disabler went on (1/1).
        var pad = await BuildTimerStackAsync();
        var disabler = await AddDisablerAsync();
        var fired = new List<long>();

        await RunUntilAsync(13_550, pad, fired);
        await disabler.Logic.OnUseAsync(Owner, 0, Ct);
        await RunUntilAsync(16_900, pad, fired);

        _room.Harness.State.ItemsById.Remove((RoomObjectId)DISABLER);
        await disabler.Logic.OnDetachAsync(Ct);
        await RunUntilAsync(19_000, pad, fired);

        fired.Skip(3).Should().Equal(17_500, 18_500);
    }

    [Fact]
    public async Task A_player_without_rights_cannot_switch_it()
    {
        var disabler = await AddDisablerAsync();

        await disabler.Logic.OnUseAsync(
            ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1),
            0,
            Ct
        );

        disabler.Logic.GetState().Should().Be(0);
    }

    private async Task<IRoomFloorItem> AddDisablerAsync()
    {
        var disabler = _room.AddFloorItem(
            DISABLER,
            6,
            6,
            "wired_disable",
            createLogic: (factory, ctx) => new FurnitureWiredDisableLogic(factory, ctx)
        );

        await disabler.Logic.OnAttachAsync(Ct);

        return disabler;
    }

    // Clicking CLICK_ME toggles PAD.
    private async Task<IRoomFloorItem> BuildStackAsync()
    {
        _room.AddFloorItem(CLICK_ME, 7, 4);
        var pad = _room.AddFloorItem(PAD, 3, 3);
        _room.AddBox<WiredTriggerClickFurni>(TRIGGER, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionToggleItemState>(EFFECT, 0, 0, "wf_act_toggle_state");
        (await SaveTriggerAsync()).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams: [0],
                stuffIds: [PAD],
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
                CausedBy = Owner,
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );
        await TickAsync(1);

        return pad;
    }

    // A 1 s timer toggles PAD. First seen at 10 050, it fires from 11 500 on, every second.
    private async Task<IRoomFloorItem> BuildTimerStackAsync()
    {
        var pad = _room.AddFloorItem(PAD, 3, 3);
        _room.AddBox<WiredTriggerPeriodically>(TRIGGER, 0, 0, "wf_trg_periodically");
        _room.AddBox<WiredActionToggleItemState>(EFFECT, 0, 0, "wf_act_toggle_state");
        (await _room.SaveAsync<UpdateTriggerMessage>(TRIGGER, intParams: [2])).Should().BeTrue();
        (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams: [0],
                stuffIds: [PAD],
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
                CausedBy = Owner,
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );

        return pad;
    }

    // Wired ticks every 50 ms up to `until`; each PAD toggle is a firing at that tick.
    private async Task RunUntilAsync(long until, IRoomFloorItem pad, List<long> fired)
    {
        while (_now < until)
        {
            var before = pad.Logic.GetState();
            _now += 50;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);

            if (pad.Logic.GetState() != before)
                fired.Add(_now);
        }
    }

    private Task<bool> SaveTriggerAsync() =>
        _room.SaveAsync<UpdateTriggerMessage>(
            TRIGGER,
            stuffIds: [CLICK_ME],
            furniSources:
            [
                [WiredFurniSourceType.SelectedItems],
            ]
        );

    private async Task ClickAsync()
    {
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(2);
    }

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
