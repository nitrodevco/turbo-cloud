using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Furniture.Floor;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Room Initialized (trigger code 28, <c>wf_trg_room_initializes</c>, AS3 1745
/// <c>triggerconfs/RoomInitialized.as</c>): "This box triggers once when the room initializes, and
/// it is the first to trigger" (<c>wiredfurni.params.room_initialized.usage_info</c>). Ours had no
/// such box, so it stayed plain furniture.
/// </summary>
public sealed class WiredRoomInitializedTests
{
    private const int TRIGGER = 1;
    private const int EFFECT = 2;
    private const int LAMP = 20;
    private const int CLICK_ME = 21;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public void The_box_is_trigger_code_28()
    {
        var item = _room.AddFloorItem(40, 1, 1, "wf_trg_room_initializes");

        var instance = _room.Harness.LogicProvider.CreateLogicInstance(
            "wf_trg_room_initializes",
            new RoomFloorItemContext(_room.Harness.Room, item)
        );

        instance.Should().BeOfType<WiredTriggerRoomInitialized>();
        ((FurnitureWiredLogic)instance!).WiredCode.Should().Be(28);
    }

    [Fact]
    public async Task It_fires_once_on_the_rooms_first_wired_tick()
    {
        var lamp = await StackAsync(0, 0);

        await TickAsync(1);
        lamp.Logic.GetState().Should().Be(1);

        await TickAsync(5);
        lamp.Logic.GetState().Should().Be(1);
    }

    [Fact]
    public async Task It_fires_in_the_first_tick_alongside_an_event_queued_before_it()
    {
        var lamp = await StackAsync(0, 0);
        _room.AddFloorItem(CLICK_ME, 6, 6);
        _room.AddBox<WiredTriggerClickFurni>(3, 1, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionToggleItemState>(4, 1, 0, "wf_act_toggle_state");
        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                3,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();
        await SaveToggleAsync(4);
        await StackChangedAsync(1, 0);
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);

        // Both toggles run in the first tick: lit by the initialization, put out by the click.
        await TickAsync(1);
        lamp.Logic.GetState().Should().Be(0);
    }

    [Fact]
    public async Task A_box_placed_after_the_room_initialized_waits_for_the_next_initialization()
    {
        await TickAsync(1);
        var lamp = await StackAsync(0, 0);

        await TickAsync(5);
        lamp.Logic.GetState().Should().Be(0);
    }

    private async Task<Turbo.Primitives.Rooms.Object.Furniture.Floor.IRoomFloorItem> StackAsync(
        int x,
        int y
    )
    {
        var lamp = _room.AddFloorItem(LAMP, 5, 5);
        _room.AddBox<WiredTriggerRoomInitialized>(TRIGGER, x, y, "wf_trg_room_initializes");
        _room.AddBox<WiredActionToggleItemState>(EFFECT, x, y, "wf_act_toggle_state");
        (await _room.SaveAsync<UpdateTriggerMessage>(TRIGGER)).Should().BeTrue();
        await SaveToggleAsync(EFFECT);
        await StackChangedAsync(x, y);

        return lamp;
    }

    private async Task SaveToggleAsync(int id) => (
            await _room.SaveAsync<UpdateActionMessage>(
                id,
                intParams: [0],
                stuffIds: [LAMP],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

    private Task StackChangedAsync(int x, int y) =>
        Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(x, y)],
            },
            Ct
        );

    private async Task TickAsync(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
