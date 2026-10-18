using FluentAssertions;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;
using Turbo.Rooms.Wired;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A reward box keeps who claimed what on itself, so a prize given once per user stays given.
/// The prize is written at once by its own grain; the claim waited for the room's next
/// hand-over, so a crash in between kept the prize and let the box give it again.
/// </summary>
public sealed class WiredGiveRewardClaimTests
{
    private const int PLAYER_INDEX = 5;
    private const int BOX = 2;

    private readonly WiredRoom _room = new(8, 8);
    private readonly WiredActionGiveReward _action;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public WiredGiveRewardClaimTests()
    {
        _room.Enter(PLAYER_INDEX, 1, 1);
        _action = _room.AddBox<WiredActionGiveReward>(BOX, 0, 0, "wf_act_give_reward");
    }

    [Fact]
    public async Task The_claim_is_written_before_the_prize_is_given()
    {
        _room.Harness.Fakes.Handlers["GiveBadgeAsync"] = _ => Task.FromResult(true);
        await SaveOncePerUserAsync();

        await ExecuteAsync();

        var calls = _room.Harness.Fakes.Log.Calls.ToList();
        var claimed = calls.FindIndex(x =>
            x.Method == "EnqueueDirtyItemAsync"
            && x.Args[1] is RoomItemSnapshot item
            && item.ObjectId == (RoomObjectId)BOX
            && item.ExtraData.Contains("wired_rewards")
        );
        var flushed = calls.FindIndex(x => x.Method == "FlushNowAsync");
        var granted = calls.FindIndex(x => x.Method == "GiveBadgeAsync");

        claimed.Should().BeGreaterThanOrEqualTo(0, "the claim is handed over at once");
        flushed.Should().BeGreaterThan(claimed);
        granted.Should().BeGreaterThan(flushed, "the claim is written before the prize");
    }

    [Fact]
    public async Task A_prize_that_was_not_given_is_not_counted_as_claimed()
    {
        _room.Harness.Fakes.Handlers["GiveBadgeAsync"] = _ => Task.FromResult(false);
        await SaveOncePerUserAsync();

        await ExecuteAsync();
        await ExecuteAsync();

        _room.Harness.Fakes.Log.Calls.Count(x => x.Method == "GiveBadgeAsync").Should().Be(2);
    }

    private async Task SaveOncePerUserAsync() => (
            await _room.SaveAsync<UpdateActionMessage>(
                BOX,
                intParams: [(int)WiredRewardIntervalType.Once, 0, 0, 1],
                playerSources:
                [
                    [WiredPlayerSourceType.TriggeredUser],
                ],
                stringParam: "0,ACH_Test1,100"
            )
        ).Should().BeTrue();

    private Task<bool> ExecuteAsync()
    {
        var ctx = new WiredExecutionContext(_room.Harness.Room) { CancellationToken = Ct };

        ctx.Selected.SelectedAvatarIds.Add(PLAYER_INDEX);

        return _action.ExecuteAsync(ctx, Ct);
    }
}
