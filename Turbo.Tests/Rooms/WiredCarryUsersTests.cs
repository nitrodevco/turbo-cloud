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
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The carry-users add-on with an animation time (tester video 2026-10-23, Habbo beside ours:
/// a tile raised by Set Furni Altitude and a seat moved along by a move effect, each with the
/// user on it). Official carries the user as one slide with the furni's animation time, on its
/// own tile when only the height changes, at the height it is drawn at (a sitter at its seat's
/// sit height), and every UserUpdate while the slide runs says "wf" so the client lets the
/// slide finish (official client capture 2026-10-17T18-25-30: "23,23,0.55 -> 23,23,0.65" 14/14,
/// "/wf/sit 0.55 0/" 16/16). Ours left an only-raised user to the next avatar tick, which put
/// it up at once, and a carried user's next UserUpdate put it down at the end of a slide still
/// running.
/// </summary>
public sealed class WiredCarryUsersTests
{
    private const int CLICK_ME = 20;
    private const int FURNI = 21;
    private const int USER = 5;
    private const int ANIMATION_MS = 1000;

    // The altitude box's "set" operator, 2.00 tiles; the move box's "move west" arrow.
    private const int SET = 2;
    private const int TWO_TILES = 200;
    private const int MOVE_WEST = 7;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Fact]
    public async Task A_user_on_a_raised_furni_slides_up_with_it_on_its_tile()
    {
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(FURNI, 3, 3);
        var user = _room.Enter(USER, 3, 3, "rider");
        await SettleAsync(user);

        await BuildAsync(effect: async box =>
        {
            _room.AddBox<WiredActionSetAltitude>(box, 0, 0, "wf_act_set_altitude");

            return await _room.SaveAsync<UpdateActionMessage>(
                box,
                intParams: [TWO_TILES, SET],
                stuffIds: [FURNI],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            );
        });

        var (moves, updates) = await ClickAsync();

        moves
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    SourceX = 3,
                    SourceY = 3,
                    TargetX = 3,
                    TargetY = 3,
                    MoveType = SlideAvatarMoveType.Slide,
                    AnimationTime = ANIMATION_MS,
                }
            );
        ((double)moves[0].SourceZ).Should().Be(1.0);
        ((double)moves[0].TargetZ).Should().Be(3.0);
        AssertSaidWithWfWhileSliding(updates);
    }

    [Fact]
    public async Task A_sitter_carried_with_its_seat_slides_at_its_sit_height_and_stays_seated()
    {
        _room.AddFloorItem(CLICK_ME, 7, 4);
        _room.AddFloorItem(FURNI, 3, 3, canWalk: false, canSit: true);
        var user = _room.Enter(USER, 3, 3, "sitter");
        await SettleAsync(user);
        user.HasStatus(AvatarStatusType.Sit).Should().BeTrue();

        await BuildAsync(effect: async box =>
        {
            _room.AddBox<WiredActionMoveRotateFurni>(box, 0, 0, "wf_act_move_rotate");

            return await _room.SaveAsync<UpdateActionMessage>(
                box,
                intParams: [MOVE_WEST, 0],
                stuffIds: [FURNI],
                furniSources:
                [
                    [WiredFurniSourceType.SelectedItems],
                ],
                definitionSpecifics: [0]
            );
        });

        var (moves, updates) = await ClickAsync();

        moves
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new
                {
                    SourceX = 3,
                    SourceY = 3,
                    TargetX = 2,
                    TargetY = 3,
                    AnimationTime = ANIMATION_MS,
                }
            );
        // The seat stands on the floor and seats at its full height (1.00): drawn there, not
        // at the floor under it.
        ((double)moves[0].SourceZ)
            .Should()
            .Be(1.0);
        ((double)moves[0].TargetZ).Should().Be(1.0);
        AssertSaidWithWfWhileSliding(updates);
        updates.Should().OnlyContain(x => x.Update.Status.Contains("sit"));
        user.HasStatus(AvatarStatusType.Sit).Should().BeTrue();
    }

    /// <summary>
    /// Updates in the slide's second say "wf" first; the one after it ends says the same
    /// without it, once.
    /// </summary>
    private static void AssertSaidWithWfWhileSliding(
        List<(long AtMs, RoomAvatarSnapshot Update)> updates
    )
    {
        var during = updates.Where(x => x.AtMs < ANIMATION_MS).ToList();
        var after = updates.Where(x => x.AtMs >= ANIMATION_MS).ToList();

        during.Should().NotBeEmpty().And.OnlyContain(x => x.Update.Status.StartsWith("/wf/"));
        after.Should().ContainSingle().Which.Update.Status.Should().NotContain("wf");
    }

    private async Task SettleAsync(RoomPlayerAvatar user)
    {
        await _room.Map.InvokeAvatarAsync(user, Ct);
        await AvatarTickAsync();
    }

    /// <summary>
    /// Click furni -> the effect on <see cref="FURNI"/>, with the carry-users add-on (users
    /// standing on the furni, everyone in the room) and a 1000 ms animation time.
    /// </summary>
    private async Task BuildAsync(Func<int, Task<bool>> effect)
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

        (await effect(2)).Should().BeTrue();

        _room.AddBox<WiredAddonCarryUsers>(3, 0, 0, "wf_xtra_mov_carry_users");
        (
            await _room.SaveAsync<UpdateAddonMessage>(
                3,
                intParams: [(int)WiredCarryUserType.StandingOnFurni],
                playerSources:
                [
                    [WiredPlayerSourceType.AllRoomUsers],
                ]
            )
        ).Should().BeTrue();

        _room.AddBox<WiredAddonAnimationTime>(4, 0, 0, "wf_xtra_anim_time");
        var animationTimeSaved = await _room.SaveAsync<UpdateAddonMessage>(
            4,
            intParams: [ANIMATION_MS]
        );

        animationTimeSaved.Should().BeTrue();

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

    /// <summary>
    /// The user's wired moves from a click, and its UserUpdates over the avatar ticks of the next
    /// two seconds with how long after the click each went out.
    /// </summary>
    private async Task<(
        List<WiredUserMovementSnapshot> Moves,
        List<(long AtMs, RoomAvatarSnapshot Update)> Updates
    )> ClickAsync()
    {
        var movesBefore = _room.Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>().Count();
        var clickedAt = _room.Harness.NowMs();

        await _room
            .FloorItem(CLICK_ME)
            .Logic.OnClickAsync(ActionContext.CreateForPlayer((PlayerId)105, (RoomId)1), 0, Ct);
        await TickAsync(3);

        var moves = _room
            .Harness.Fakes.SentToRoom<WiredMovementsMessageComposer>()
            .Skip(movesBefore)
            .SelectMany(x => x.Users)
            .Where(x => x.ObjectId == USER)
            .ToList();
        var updates = new List<(long, RoomAvatarSnapshot)>();

        for (var i = 0; i < 4; i++)
        {
            var before = _room.Harness.Fakes.AvatarUpdates().Count;

            await AvatarTickAsync();

            updates.AddRange(
                _room
                    .Harness.Fakes.AvatarUpdates()
                    .Skip(before)
                    .SelectMany(x => x)
                    .Where(x => x.ObjectId == USER)
                    .Select(x => (_room.Harness.NowMs() - clickedAt, x))
            );
        }

        return (moves, updates);
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
