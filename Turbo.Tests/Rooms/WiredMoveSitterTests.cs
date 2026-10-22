using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A user sitting on a seat that wired moves, without the carry-users add-on (official client
/// capture, 2026-10-22T22-09-06, 22-09-30, 22-10-35, 22-11-14: a sofa with a sitter, moved and
/// turned by move-rotate effects). A move leaves the sitter on its tile, stood up by one
/// UserUpdate on the next user tick, with no move of its own; a turn in place keeps it seated,
/// turned the seat's new way, again by one UserUpdate.
/// </summary>
public sealed class WiredMoveSitterTests
{
    private const int CLICK_ME = 20;
    private const int SEAT = 21;
    private const int SITTER = 5;

    // The editor's "move west" arrow and its "rotate clockwise" option.
    private const int MOVE_WEST = 7;
    private const int NO_MOVE = 0;
    private const int ROTATE_CW = 1;

    private readonly WiredRoom _room = new(8, 8);
    private readonly RoomPlayerAvatar _sitter;
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    public WiredMoveSitterTests()
    {
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(SEAT, 3, 3, canWalk: false, canSit: true);
        _sitter = _room.Enter(SITTER, 3, 3, "sitter");
    }

    // Official: 6/6 without the add-on and 5/5 with Execute In Order, one UserUpdate "//" in place.
    [Fact]
    public async Task A_seat_moved_away_leaves_its_sitter_standing_where_it_sat()
    {
        await BuildAsync((MOVE_WEST, NO_MOVE), (MOVE_WEST, NO_MOVE));
        await SeatAsync();

        var sent = await ClickAsync();

        (_room.FloorItem(SEAT).X, _room.FloorItem(SEAT).Y).Should().Be((1, 3));
        sent.Should().ContainSingle().Which.Should().BeEquivalentTo(new { X = 3, Y = 3 });
        sent[0].Status.Should().NotContain("sit");
        _sitter.HasStatus(AvatarStatusType.Sit).Should().BeFalse();
        _room
            .Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>()
            .SelectMany(x => x.Users)
            .Should()
            .BeEmpty("the move does not carry the sitter");
    }

    // Official: 4/4, the sitter turned with the seat by one UserUpdate, "/sit 1.2 0/".
    [Fact]
    public async Task A_seat_turned_in_place_keeps_its_sitter_seated_facing_its_new_way()
    {
        await BuildAsync((NO_MOVE, ROTATE_CW));
        await SeatAsync();

        var sent = await ClickAsync();

        var seat = _room.FloorItem(SEAT);

        seat.Rotation.Should().Be(Rotation.East);
        sent.Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    X = 3,
                    Y = 3,
                    BodyRotation = Rotation.East,
                    HeadRotation = Rotation.East,
                }
            );
        sent[0].Status.Should().Contain("sit");
    }

    private async Task SeatAsync()
    {
        await _room.Map.InvokeAvatarAsync(_sitter, Ct);

        _sitter.HasStatus(AvatarStatusType.Sit).Should().BeTrue();
        await AvatarTickAsync();
    }

    /// <summary>The sitter's UserUpdates from a click to the user ticks after it.</summary>
    private async Task<List<RoomAvatarSnapshot>> ClickAsync()
    {
        var before = _room.Harness.Fakes.AvatarUpdates().Count;

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(3);

        for (var i = 0; i < 4; i++)
            await AvatarTickAsync();

        return
        [
            .. _room
                .Harness.Fakes.AvatarUpdates()
                .Skip(before)
                .SelectMany(x => x)
                .Where(x => x.ObjectId == SITTER),
        ];
    }

    private async Task BuildAsync(params (int Move, int Rotate)[] effects)
    {
        _room.AddBox<WiredTriggerClickFurni>(1, 0, 0, "wf_trg_click_furni");

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

        var box = 2;

        foreach (var (move, rotate) in effects)
        {
            _room.AddBox<WiredActionMoveRotateFurni>(box, 0, 0, "wf_act_move_rotate");

            (
                await _room.SaveAsync<UpdateActionMessage>(
                    box,
                    intParams: [move, rotate],
                    stuffIds: [SEAT],
                    furniSources:
                    [
                        [WiredFurniSourceType.SelectedItems],
                    ],
                    definitionSpecifics: [0]
                )
            ).Should().BeTrue();

            box++;
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

    private Task AvatarTickAsync()
    {
        _room.Clock.AdvanceMs(500);

        return _room.Harness.Room.AvatarTickSystem.ProcessAvatarsAsync(_room.Harness.NowMs(), Ct);
    }
}
