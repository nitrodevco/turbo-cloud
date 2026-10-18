using System.Collections;
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
using Turbo.Primitives.Rooms.Snapshots;
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
        SentToRoom<ConfigurationItemStatesMessageComposer>().Should().BeEmpty();
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

        ((IDictionary)RoomHarness.GetMember(_room.Harness.State, "ItemsById")!).Remove(
            (RoomObjectId)DISABLER
        );
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
        var addsBefore = SentToRoom<ObjectAddMessageComposer>().Count();

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
                SentToRoom<ObjectAddMessageComposer>().Skip(addsBefore).Single().FloorItem.StuffData
        )
            .Data.Should()
            .Be("0", "the room is told of it off");
        await ClickAsync();
        pad.Logic.GetState().Should().Be(1, "wired fires with the re-placed disabler");
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

    private IEnumerable<T> SentToRoom<T>() =>
        _room
            .Harness.Fakes.Log.Calls.SelectMany(x => x.Args)
            .OfType<RoomOutboundSnapshot>()
            .SelectMany(x => x.Composers)
            .OfType<T>();
}
