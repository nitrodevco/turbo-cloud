using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A stack's effects run in a random order unless "Execute In Order" (wf_xtra_exec_in_order)
/// keeps them in stacking order. A lamp stands against the west wall; the stack moves it west,
/// then east. West first is blocked by the wall and leaves the lamp one tile out; east first
/// brings it back to the wall.
/// </summary>
public sealed class WiredEffectOrderTests
{
    private const int CLICK_ME = 20;
    private const int LAMP = 21;

    // The editor's "move west" and "move east" arrows.
    private const int MOVE_WEST = 7;
    private const int MOVE_EAST = 5;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredEffectOrderTests()
    {
        _room.Enter(5, 6, 6);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(LAMP, 0, 3);
    }

    [Fact]
    public async Task Without_an_addon_the_effects_run_in_a_random_order()
    {
        await BuildAsync(inOrder: false);

        var endedAt = new HashSet<int>();

        for (var i = 0; i < 40; i++)
            endedAt.Add(await ClickFromTheWallAsync());

        endedAt.Should().BeEquivalentTo([0, 1]);
    }

    [Fact]
    public async Task Execute_in_order_runs_them_in_stacking_order()
    {
        await BuildAsync(inOrder: true);

        for (var i = 0; i < 20; i++)
            (await ClickFromTheWallAsync())
                .Should()
                .Be(1, $"click {i + 1} moved west, against the wall, before east");
    }

    private async Task<int> ClickFromTheWallAsync()
    {
        _room.Map.MoveFloorItem(_room.FloorItem(LAMP), _room.Map.ToIdx(0, 3));

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(3);

        return _room.FloorItem(LAMP).X;
    }

    private async Task BuildAsync(bool inOrder)
    {
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");

        if (inOrder)
            _room.AddBox<WiredAddonExecuteInOrder>(2, 0, 0, "wf_xtra_exec_in_order");

        _room.AddBox<WiredActionMoveRotateFurni>(3, 0, 0, "wf_act_move_rotate");
        _room.AddBox<WiredActionMoveRotateFurni>(4, 0, 0, "wf_act_move_rotate");

        (
            await _room.SaveAsync<UpdateTriggerMessage>(
                1,
                stuffIds: [CLICK_ME],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ]
            )
        ).Should().BeTrue();

        foreach (var (box, movement) in new[] { (3, MOVE_WEST), (4, MOVE_EAST) })
        {
            (
                await _room.SaveAsync<UpdateActionMessage>(
                    box,
                    intParams: [movement, 0],
                    stuffIds: [LAMP],
                    furniSources:
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ],
                    definitionSpecifics: [0]
                )
            ).Should().BeTrue();
        }

        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );
        await TickAsync(1);
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
