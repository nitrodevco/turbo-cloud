using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// Chase and Flee as Habbo runs them (official client capture 2026-10-24, W1 lab, own room, a
/// block and one user). Chase steps toward a user 2 or 3 tiles away in the block's row or column
/// (action.11.R1, 16/16) and takes a random straight step for one 4 or 5 tiles away
/// (action.11.R2, 23 firings) or off its row and column, diagonal neighbour included
/// (action.11.R3, 31 firings); an orthogonal neighbour is caught, no move (action.11.B1, 3/3).
/// Flee steps away from a user 2 tiles away in line (action.12.R1, 8/8); with nobody in reach it
/// keeps the way it last fled (action.12.R2, 15/15), turning when that way is blocked
/// (action.12.B14, blocked E went N 5 / S 3 of 8); a neighbour is caught (action.12.B1, 3/3). Ours chased
/// anyone anywhere in the room and fled only when someone was near.
/// </summary>
public sealed class WiredChaseFleeTests
{
    private const int BOX = 7;
    private const int BLOCK = 30;
    private const int USER = 5;
    private const int LAMP = 31;

    private readonly WiredRoom _room = new(12, 12);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // action.11.R1
    [Theory]
    [InlineData(3, 5)]
    [InlineData(5, 3)]
    [InlineData(5, 8)]
    public async Task Chase_steps_toward_a_user_three_tiles_away_in_line(int userX, int userY)
    {
        _room.Enter(USER, userX, userY);
        var chase = await PlaceAsync<WiredActionChaseHabbo>("wf_act_chase", 5, 5);

        await RunAsync(chase);

        Block().Should().Be((5 + Math.Sign(userX - 5), 5 + Math.Sign(userY - 5)));
    }

    // action.11.R2 (5 tiles in line), action.11.R3 (off the line, and the diagonal neighbour).
    [Theory]
    [InlineData(10, 5)]
    [InlineData(7, 4)]
    [InlineData(6, 6)]
    public async Task Chase_steps_at_random_for_a_user_out_of_reach_or_off_the_line(
        int userX,
        int userY
    )
    {
        _room.Enter(USER, userX, userY);
        var chase = await PlaceAsync<WiredActionChaseHabbo>("wf_act_chase", 5, 5);
        var ways = new HashSet<(int, int)>();

        for (var i = 0; i < 40; i++)
        {
            _room.Map.MoveFloorItem(_room.FloorItem(BLOCK), _room.Map.ToIdx(5, 5));

            await RunAsync(chase);

            ways.Add(Block());
        }

        ways.Should().BeSubsetOf([(5, 4), (6, 5), (5, 6), (4, 5)]);
        ways.Should().HaveCountGreaterThan(1, "the steps are random, not toward the user");
    }

    // action.11.B1
    [Fact]
    public async Task Chase_does_not_move_onto_an_adjacent_user()
    {
        _room.Enter(USER, 6, 5);
        var chase = await PlaceAsync<WiredActionChaseHabbo>("wf_act_chase", 5, 5);

        await RunAsync(chase);

        Block().Should().Be((5, 5));
    }

    // Sheet action.11.B1 (LAB-W1 08-10-45, 6/6): the user 3 tiles north in line, a lamp on the
    // step toward them. Chase never waits: a random straight step onto a free tile. Ours stayed.
    [Fact]
    public async Task Chase_blocked_toward_its_user_steps_at_random_onto_a_free_tile()
    {
        _room.Enter(USER, 5, 2);
        AddLamp(LAMP, 5, 4);
        var chase = await PlaceAsync<WiredActionChaseHabbo>("wf_act_chase", 5, 5);
        var ways = new HashSet<(int, int)>();

        for (var i = 0; i < 40; i++)
        {
            _room.Map.MoveFloorItem(_room.FloorItem(BLOCK), _room.Map.ToIdx(5, 5));

            await RunAsync(chase);

            ways.Add(Block());
        }

        ways.Should().BeSubsetOf([(6, 5), (5, 6), (4, 5)], "it never waits nor enters the lamp");
        ways.Should().HaveCountGreaterThan(1);
    }

    // Sheet action.11.R10 (LAB-W1, 23 firings): nobody in reach, in a corner with two free tiles,
    // it moved every firing. Ours drew one of four ways and waited when that one was off the map.
    [Fact]
    public async Task Chase_with_nobody_in_reach_moves_every_firing_even_in_a_corner()
    {
        var chase = await PlaceAsync<WiredActionChaseHabbo>("wf_act_chase", 0, 0);

        for (var i = 0; i < 20; i++)
        {
            _room.Map.MoveFloorItem(_room.FloorItem(BLOCK), _room.Map.ToIdx(0, 0));

            await RunAsync(chase);

            Block().Should().BeOneOf((1, 0), (0, 1));
        }
    }

    // Sheet action.12.R11 (LAB-W1 09-31-35, 3/3 first moves, 11/11 moves): a fresh flee box on a
    // block that never fled, nobody in line, moves east and keeps going east. Ours did nothing.
    [Fact]
    public async Task Flee_that_never_fled_heads_east()
    {
        _room.Enter(USER, 8, 8);
        var flee = await PlaceAsync<WiredActionFleeHabbo>("wf_act_flee", 5, 5);

        await RunAsync(flee);
        await RunAsync(flee);

        Block().Should().Be((7, 5));
    }

    // action.12.R1, then action.12.R2: the user leaves the line, the block keeps fleeing east.
    [Fact]
    public async Task Flee_steps_away_then_keeps_the_way_it_fled()
    {
        var user = _room.Enter(USER, 3, 5);
        var flee = await PlaceAsync<WiredActionFleeHabbo>("wf_act_flee", 5, 5);

        await RunAsync(flee);

        Block().Should().Be((6, 5));

        Relocate(user, 3, 9);

        await RunAsync(flee);
        await RunAsync(flee);

        Block().Should().Be((8, 5));
    }

    // action.12.R3 / B14: the way it fled runs into the map's edge; it turns a quarter to a
    // random side, N 5 and S 3 of 8 wherever the user stood, never back (ours always turned N).
    [Fact]
    public async Task Flee_turns_to_a_random_side_when_the_way_it_fled_is_blocked()
    {
        _room.Enter(USER, 3, 9);
        var flee = await PlaceAsync<WiredActionFleeHabbo>("wf_act_flee", 11, 5);
        var turnedTo = new HashSet<(int, int)>();

        for (var i = 0; i < 30; i++)
        {
            _room.Map.MoveFloorItem(_room.FloorItem(BLOCK), _room.Map.ToIdx(11, 5));
            await flee.LoadWiredAsync(Ct);
            await RunAsync(flee);

            turnedTo.Add(Block());
        }

        turnedTo.Should().BeEquivalentTo([(11, 4), (11, 6)]);
    }

    // action.12.B1
    [Fact]
    public async Task Flee_does_not_move_from_an_adjacent_user()
    {
        _room.Enter(USER, 5, 6);
        var flee = await PlaceAsync<WiredActionFleeHabbo>("wf_act_flee", 5, 5);

        await RunAsync(flee);

        Block().Should().Be((5, 5));
    }

    private void Relocate(Turbo.Rooms.Object.Avatars.Player.RoomPlayerAvatar user, int x, int y)
    {
        _room.Map.RemoveAvatarAtIdx(user, _room.Map.ToIdx(user.X, user.Y), false);
        user.SetPosition(x, y);
        _room.Map.AddAvatarAtIdx(user, _room.Map.ToIdx(x, y), false);
    }

    private void AddLamp(int id, int x, int y)
    {
        var lamp = _room.AddFloorItem(id, x, y, canWalk: false);

        lamp.GetType()
            .GetProperty(nameof(lamp.Definition))!
            .SetValue(lamp, lamp.Definition with { CanStack = false });
        _room.Map.ComputeTile(x, y);
    }

    private (int, int) Block() => (_room.FloorItem(BLOCK).X, _room.FloorItem(BLOCK).Y);

    private Task RunAsync(FurnitureWiredMoveActionLogic box) =>
        box.ExecuteAsync(
            new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct },
            Ct
        );

    private async Task<T> PlaceAsync<T>(string logicName, int x, int y)
        where T : FurnitureWiredMoveActionLogic
    {
        _room.AddFloorItem(BLOCK, x, y);

        var box = _room.AddBox<T>(BOX, 0, 11, logicName);

        (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                stuffIds: [BLOCK],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            )
        ).Should().BeTrue();

        return box;
    }
}
