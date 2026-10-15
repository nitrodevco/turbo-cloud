using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Players;
using Turbo.Primitives.Quests.Grains;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Rooms.Grains.Systems;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The Progress Reward Track (Flash <c>ActionTypeCodes.PROGRESS_REWARD_TRACK</c> 58) and Reset
/// Reward Track (<c>RESET_REWARD_TRACK</c> 59) effects, whose editors are in the client but which
/// did nothing on the server.
/// </summary>
public sealed class WiredRewardTrackTests
{
    private const int USER = 5;
    private const int PLAYER = 100 + USER;
    private const int TRIGGER = 2;
    private const int EFFECT = 3;

    private readonly WiredRoom _room = new(8, 8);
    private long _now = 10_000;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private RoomWiredSystem Wired => _room.Harness.Module<RoomWiredSystem>();

    [Theory]
    [InlineData(1, 5, true)]
    [InlineData(0, 3, false)]
    public async Task Progress_reward_track_progresses_the_triggering_users_task(
        int add,
        int score,
        bool adds
    )
    {
        _room.AddBox<WiredActionProgressRewardTrack>(EFFECT, 0, 0, "wf_act_progress_reward_track");
        (
            await _room.SaveAsync<UpdateActionMessage>(
                EFFECT,
                intParams: [add, 0, score, 1],
                stringParam: "introduction\tvisit_rooms"
            )
        )
            .Should()
            .BeTrue();

        await ClickAsync();

        var call = _room
            .Harness.Fakes.Log.Of(nameof(IPlayerRewardTrackGrain.ProgressTaskAsync))
            .Should()
            .ContainSingle()
            .Subject;
        call.Key.Should().Be((long)PLAYER);
        call.Args.Take(4).Should().Equal("introduction", "visit_rooms", score, adds);
    }

    [Fact]
    public async Task Reset_reward_track_resets_the_triggering_users_track()
    {
        _room.AddBox<WiredActionResetRewardTrack>(EFFECT, 0, 0, "wf_act_reset_reward_track");
        (await _room.SaveAsync<UpdateActionMessage>(EFFECT, stringParam: "introduction"))
            .Should()
            .BeTrue();

        await ClickAsync();

        var call = _room
            .Harness.Fakes.Log.Of(nameof(IPlayerRewardTrackGrain.ResetTrackAsync))
            .Should()
            .ContainSingle()
            .Subject;
        call.Key.Should().Be((long)PLAYER);
        call.Args[0].Should().Be("introduction");
    }

    private async Task ClickAsync()
    {
        _room.Enter(USER, 1, 1);
        _room.Enter(6, 3, 3);
        _room.AddBox<WiredTriggerClickUser>(TRIGGER, 0, 0, "wf_trg_click_user");
        (await _room.SaveAsync<UpdateTriggerMessage>(TRIGGER, intParams: [0, 0])).Should().BeTrue();
        await Wired.OnRoomEventAsync(
            new RoomWiredStackChangedEvent
            {
                RoomId = 1,
                CausedBy = ActionContext.CreateForSystem(1),
                StackIds = [_room.Map.ToIdx(0, 0)],
            },
            Ct
        );
        _now += 1_000;
        await Wired.ProcessWiredAsync(_now, dormant: false, Ct);

        await _room.Harness.Room.WiredClickAvatarAsync(
            ActionContext.CreateForPlayer((PlayerId)PLAYER, (RoomId)1),
            6,
            Ct
        );

        for (var i = 0; i < 3; i++)
        {
            _now += 1_000;
            await Wired.ProcessWiredAsync(_now, dormant: false, Ct);
        }
    }
}
