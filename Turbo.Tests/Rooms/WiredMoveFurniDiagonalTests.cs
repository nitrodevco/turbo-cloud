using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// "Move and rotate furni" with the editor's eight arrows: ids 4 to 7 are the cardinal ones
/// (move_0, move_2, move_4, move_6) and 8 to 11 the diagonal ones (move_1, move_3, move_5,
/// move_7). A click moves the lamp at (3,3) one tile.
/// </summary>
public sealed class WiredMoveFurniDiagonalTests
{
    private const int CLICK_ME = 23;
    private const int LAMP = 20;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Theory]
    [InlineData(4, 3, 2)]
    [InlineData(8, 4, 2)]
    [InlineData(9, 4, 4)]
    [InlineData(10, 2, 4)]
    [InlineData(11, 2, 2)]
    public async Task The_lamp_moves_one_tile_in_the_arrows_direction(int movement, int x, int y)
    {
        _room.Enter(5, 7, 7);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(LAMP, 3, 3);
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionMoveRotateFurni>(2, 0, 0, "wf_act_move_rotate");

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
                intParams: [movement, 0],
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

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(4);

        var lamp = _room.FloorItem(LAMP);

        (lamp.X, lamp.Y).Should().Be((x, y));
    }

    // W1 action.4.R9 (official client capture 2026-10-24T08-13-33, 8 firings each): movement 1
    // "random" goes any of the eight ways, diagonals included; 2 only along x, 3 only along y.
    // Ours drew 1 from the four straight ways only.
    [Theory]
    [InlineData(1, 8)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    public async Task A_random_movement_draws_from_the_ways_its_option_offers(
        int movement,
        int ways
    )
    {
        _room.Enter(5, 7, 7);
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(LAMP, 3, 3);
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");
        _room.AddBox<WiredActionMoveRotateFurni>(2, 0, 0, "wf_act_move_rotate");

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
                intParams: [movement, 0],
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

        var seen = new HashSet<(int, int)>();

        for (var i = 0; i < 80; i++)
        {
            _room.Map.MoveFloorItem(_room.FloorItem(LAMP), _room.Map.ToIdx(3, 3));

            await _room
                .FloorItem(CLICK_ME)
                .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
            await TickAsync(2);

            var lamp = _room.FloorItem(LAMP);

            seen.Add((lamp.X - 3, lamp.Y - 3));
        }

        seen.Should().HaveCount(ways);
        seen.Should().OnlyContain(d => Math.Abs(d.Item1) <= 1 && Math.Abs(d.Item2) <= 1);

        if (movement == 2)
            seen.Should().OnlyContain(d => d.Item2 == 0);
        if (movement == 3)
            seen.Should().OnlyContain(d => d.Item1 == 0);
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
