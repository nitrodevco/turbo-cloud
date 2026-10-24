using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Primitives.Action;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The furni movers may put a furni on the room's door tile, and "Can Perform Movements" passes
/// for that move; the next step, off the map, moves nothing and fails the check. Habbo moved a
/// block from 5,4 west onto the door at 4,4 with Move Furni (relative) [-1,0], Move and Rotate
/// [7,0,0] and Move To Direction [6,0,1], valid_moves passing each time, and from the door the
/// next step west did not move (official client capture 2026-10-24, WIN63-202610201032, explore
/// 07-44-36 and 17-52-52, wired matrix action.33.B5 / action.4.B5 / action.13.B5 /
/// condition.39.B5, 3/3 + 3/3); a step off the east edge likewise moved nothing and failed the
/// check (17-51-58, action.33.B4 / action.4.B4 / action.13.B4 / condition.39.B4, 3/3). The
/// harness room's door is at 0,0. A switch the stack toggles shows whether the check passed.
/// </summary>
public sealed class WiredMoverDoorTileTests
{
    private const int TRIGGER = 1;
    private const int MOVER = 2;
    private const int CONDITION = 3;
    private const int TOGGLE = 4;
    private const int CLICK_ME = 20;
    private const int BLOCK = 21;
    private const int SWITCH = 22;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Theory]
    [InlineData("wf_act_rel_mov", new[] { -1, 0 })]
    [InlineData("wf_act_move_rotate", new[] { 7, 0, 0 })]
    [InlineData("wf_act_move_to_dir", new[] { 6, 0, 1 })]
    public async Task A_mover_puts_a_furni_on_the_door_tile_and_no_further(string mover, int[] west)
    {
        _room.AddFloorItem(CLICK_ME, 6, 6);
        _room.AddFloorItem(SWITCH, 6, 3);
        _room.AddFloorItem(BLOCK, 1, 0);
        await BuildAsync(mover, west);

        await ClickAsync();
        (_room.FloorItem(BLOCK).X, _room.FloorItem(BLOCK).Y).Should().Be((0, 0));
        SwitchState().Should().Be(1, "the move onto the door passes the check");

        await ClickAsync();
        (_room.FloorItem(BLOCK).X, _room.FloorItem(BLOCK).Y).Should().Be((0, 0));
        SwitchState().Should().Be(1, "the step off the map fails the check");
    }

    private int SwitchState() => _room.FloorItem(SWITCH).Logic.GetState();

    private async Task BuildAsync(string mover, int[] intParams)
    {
        _room.AddBox<WiredTriggerClickFurni>(TRIGGER, 7, 7, "wf_trg_click_furni");
        _room.AddBox<WiredConditionValidMoves>(CONDITION, 7, 7, "wf_cnd_valid_moves");
        _room.AddBox<WiredActionToggleItemState>(TOGGLE, 7, 7, "wf_act_toggle_state");
        _room.AddFloorItem(
            MOVER,
            7,
            7,
            mover,
            createLogic: (factory, ctx) =>
                mover switch
                {
                    "wf_act_rel_mov" => Box<WiredActionRelativeMove>(factory, ctx),
                    "wf_act_move_rotate" => Box<WiredActionMoveRotateFurni>(factory, ctx),
                    _ => Box<WiredActionMoveToDirection>(factory, ctx),
                }
        );

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
        (await _room.SaveAsync<UpdateConditionMessage>(CONDITION, definitionSpecifics: [0]))
            .Should()
            .BeTrue();
        await SaveActionAsync(TOGGLE, [0], SWITCH);
        await SaveActionAsync(MOVER, intParams, BLOCK);

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(7, 7)],
            },
            Ct
        );
        await TickAsync(1);
    }

    private IRoomObjectLogic Box<T>(IStuffDataFactory factory, IRoomFloorItemContext ctx)
        where T : class, IRoomObjectLogic =>
        ActivatorUtilities.CreateInstance<T>(_room.Harness.Services, _room.Grains, factory, ctx);

    private async Task SaveActionAsync(int boxId, int[] intParams, int furni) => (
            await _room.SaveAsync<UpdateActionMessage>(
                boxId,
                intParams: intParams,
                stuffIds: [furni],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

    private async Task ClickAsync()
    {
        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(4);
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
