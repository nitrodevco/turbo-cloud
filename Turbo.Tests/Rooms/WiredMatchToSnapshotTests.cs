using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Rooms.Grains.Modules;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Match furni to position &amp; state" with its four boxes (state, direction, position,
/// altitude) as the editor saves them: the lamp saved at (3,3) facing north is moved and turned
/// by a player, and a click puts it back.
/// </summary>
public sealed class WiredMatchToSnapshotTests
{
    private const int CLICK_ME = 23;
    private const int LAMP = 20;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    private RoomFurniModule Furni => _room.Harness.Module<RoomFurniModule>();

    [Fact]
    public async Task A_moved_and_turned_furni_goes_back_to_its_saved_tile_and_direction()
    {
        await BuildAsync([0, 1, 1, 0]);
        var lamp = _room.FloorItem(LAMP);

        (
            await Furni.MoveFloorItemAsync(
                ActionContext.CreateForSystem(1),
                lamp,
                _room.Map.ToIdx(6, 1),
                null,
                Rotation.East,
                true,
                Ct
            )
        )
            .Should()
            .BeTrue();

        await ClickAsync();

        (lamp.X, lamp.Y, lamp.Rotation).Should().Be((3, 3, Rotation.North));
    }

    [Fact]
    public async Task It_goes_back_every_time_it_is_moved_away()
    {
        await BuildAsync([0, 0, 1, 0]);
        var lamp = _room.FloorItem(LAMP);

        foreach (var (x, y) in new[] { (6, 1), (1, 6) })
        {
            await Furni.MoveFloorItemAsync(
                ActionContext.CreateForSystem(1),
                lamp,
                _room.Map.ToIdx(x, y),
                null,
                null,
                true,
                Ct
            );

            await ClickAsync();

            (lamp.X, lamp.Y).Should().Be((3, 3));
        }
    }

    [Fact]
    public async Task With_every_box_ticked_a_furni_moved_onto_a_stack_comes_back_down()
    {
        await BuildAsync([1, 1, 1, 1]);
        var lamp = _room.FloorItem(LAMP);
        _room.AddFloorItem(30, 6, 1);

        (
            await Furni.MoveFloorItemAsync(
                ActionContext.CreateForSystem(1),
                lamp,
                _room.Map.ToIdx(6, 1),
                null,
                Rotation.South,
                true,
                Ct
            )
        )
            .Should()
            .BeTrue();
        await lamp.Logic.SetStateAsync(1);
        lamp.Z.ToInt().Should().BeGreaterThan(0);

        await ClickAsync();

        (lamp.X, lamp.Y, lamp.Z, lamp.Rotation, lamp.Logic.GetState())
            .Should()
            .Be((3, 3, Altitude.Zero, Rotation.North, 0));
    }

    private async Task BuildAsync(int[] intParams)
    {
        _room.Enter(5, 7, 7);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(LAMP, 3, 3);
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionMatchToSnapshot>(2, 0, 0, "wf_act_match_to_sshot");

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
        (
            await _room.SaveAsync<UpdateActionMessage>(
                2,
                intParams: intParams,
                stuffIds: [LAMP],
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
        await TickAsync(1);
    }

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
