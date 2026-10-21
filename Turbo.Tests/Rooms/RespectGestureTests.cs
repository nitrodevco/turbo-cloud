using FluentAssertions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Grains.Respect;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// A respect given in a room shows as the target's bubble and the giver's thumbs up. The client
/// sends only RespectUser (AS3 SessionDataManager.giveRespect), so the gesture is the room's
/// Expression 7 (AvatarExpressionEnum.RESPECT) for the giver.
/// </summary>
public sealed class RespectGestureTests
{
    private static readonly ActionContext Giver = ActionContext.CreateForPlayer(9, 1);

    private readonly LiveRoomHarness _harness = new(10, 10);

    public RespectGestureTests()
    {
        AddPlayer(new RoomPlayerAvatar { ObjectId = 5, PlayerId = 9 }, 2, 2);
        AddPlayer(new RoomPlayerAvatar { ObjectId = 6, PlayerId = 10 }, 3, 3);
    }

    private void AddPlayer(RoomPlayerAvatar avatar, int x, int y)
    {
        avatar.UseName($"player{avatar.PlayerId}");
        avatar.SetPosition(x, y);
        _harness.State.AvatarsByObjectId[avatar.ObjectId] = avatar;
        _harness.State.AvatarsByPlayerId[avatar.PlayerId] = avatar.ObjectId;
        _harness.Module<Turbo.Rooms.Grains.Modules.RoomMapModule>().AddAvatar(avatar, false);
    }

    private List<object> Sent() =>
        _harness
            .Fakes.Log.Calls.SelectMany(x => x.Args)
            .OfType<RoomOutboundSnapshot>()
            .SelectMany(x => x.Composers)
            .Where(x => x is RespectNotificationMessageComposer or ExpressionMessageComposer)
            .Cast<object>()
            .ToList();

    [Fact]
    public async Task The_giver_gives_the_thumbs_up_after_the_targets_bubble()
    {
        _harness.Fakes.Handlers["ExecuteAsync"] = _ =>
            Task.FromResult(
                new HumanRespectOperationResult { Accepted = true, TargetRespectTotal = 4 }
            );

        (await _harness.Room.RespectPlayerAsync(Giver, 10, CancellationToken.None))
            .Should()
            .BeTrue();

        var sent = Sent();
        sent.Should().HaveCount(2);
        sent[0]
            .Should()
            .BeOfType<RespectNotificationMessageComposer>()
            .Which.PlayerId.Should()
            .Be((PlayerId)10);
        var expression = sent[1].Should().BeOfType<ExpressionMessageComposer>().Subject;
        expression.ObjectId.Should().Be((RoomObjectId)5);
        expression.ExpressionType.Should().Be(AvatarExpressionType.Respect);
    }

    [Fact]
    public async Task A_refused_respect_gives_no_thumbs_up()
    {
        _harness.Fakes.Handlers["ExecuteAsync"] = _ =>
            Task.FromResult(new HumanRespectOperationResult { Accepted = false });

        (await _harness.Room.RespectPlayerAsync(Giver, 10, CancellationToken.None))
            .Should()
            .BeFalse();

        Sent().Should().BeEmpty();
    }
}
