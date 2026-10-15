using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Orleans;
using Turbo.Players.Messenger;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Enums.Messenger;
using Turbo.Primitives.Players.Messenger;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players;

/// <summary>
/// The friend list's reward track tasks of Habbo's Introduction track (official client capture
/// 2026-10-12: <c>make_friends</c> REQUEST_FRIEND, <c>send_messenger_message</c>,
/// <c>send_messenger_invite</c>, <c>follow_friend</c>, <c>set_relationship_status</c>): each counts
/// when the action went through, and not when it was refused.
/// </summary>
public sealed class MessengerRewardTrackTests
{
    private const int PLAYER = 1;
    private const int FRIEND = 2;

    private readonly Fakes _fakes = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public MessengerRewardTrackTests()
    {
        _fakes.Handlers["GetPlayerIdAsync"] = _ => Task.FromResult<PlayerId?>(FRIEND);
        _fakes.Handlers["GetSummaryAsync"] = _ =>
            Task.FromResult(
                new PlayerSummarySnapshot
                {
                    PlayerId = PLAYER,
                    Name = "me",
                    Motto = string.Empty,
                    Figure = "hd-180-1",
                    Gender = AvatarGenderType.Male,
                    AchievementScore = 0,
                    BadgesRank = 0,
                    IsOnline = true,
                    CreatedAt = DateTime.UnixEpoch,
                    LastUpdated = DateTime.UnixEpoch,
                    RespectPoints = 0,
                    RespectsLeft = 0,
                    PetRespectsLeft = 0,
                    RespectReplenishesLeft = 0,
                }
            );
    }

    [Fact]
    public async Task A_friend_request_sent_counts_and_a_refused_one_does_not()
    {
        _fakes.Handlers["SendFriendRequestAsync"] = _ =>
            Task.FromResult(new MessengerRequestFriendResult(true));
        await Service().RequestFriendAsync(PLAYER, "friend", Ct);

        _fakes.Handlers["SendFriendRequestAsync"] = _ =>
            Task.FromResult(
                new MessengerRequestFriendResult(false, FriendListErrorCodeType.YouHitFriendLimit)
            );
        await Service().RequestFriendAsync(PLAYER, "friend", Ct);

        Counted().Should().Equal(RewardTrackActionTypes.REQUEST_FRIEND);
    }

    [Fact]
    public async Task A_message_sent_counts_and_a_refused_one_does_not()
    {
        _fakes.Handlers["SendMessageAsync"] = _ =>
            Task.FromResult<InstantMessageErrorCodeType?>(null);
        await Service().SendMessageAsync(PLAYER, FRIEND, "hi", 1, Ct);

        _fakes.Handlers["SendMessageAsync"] = _ =>
            Task.FromResult<InstantMessageErrorCodeType?>(InstantMessageErrorCodeType.NotFriend);
        await Service().SendMessageAsync(PLAYER, FRIEND, "hi", 2, Ct);

        Counted().Should().Equal(RewardTrackActionTypes.SEND_MESSENGER_MESSAGE);
    }

    [Fact]
    public async Task An_invite_counts_once_when_it_reached_someone()
    {
        _fakes.Handlers["SendRoomInviteAsync"] = _ => Task.FromResult(new List<PlayerId>());
        await Service().SendRoomInviteAsync(PLAYER, [FRIEND, 3], "come", Ct);

        _fakes.Handlers["SendRoomInviteAsync"] = _ =>
            Task.FromResult(new List<PlayerId> { FRIEND });
        await Service().SendRoomInviteAsync(PLAYER, [FRIEND], "come", Ct);

        Counted().Should().Equal(RewardTrackActionTypes.SEND_MESSENGER_INVITE);
    }

    [Fact]
    public async Task Following_a_friend_counts_and_a_refused_follow_does_not()
    {
        _fakes.Handlers["FollowFriendAsync"] = _ =>
            Task.FromResult<FollowFriendErrorCodeType?>(null);
        await Service().FollowFriendAsync(PLAYER, FRIEND, Ct);

        _fakes.Handlers["FollowFriendAsync"] = _ =>
            Task.FromResult<FollowFriendErrorCodeType?>(FollowFriendErrorCodeType.HotelView);
        await Service().FollowFriendAsync(PLAYER, FRIEND, Ct);

        Counted().Should().Equal(RewardTrackActionTypes.FOLLOW_FRIEND);
    }

    [Fact]
    public async Task Setting_a_relationship_counts_and_clearing_or_a_refused_one_does_not()
    {
        _fakes.Handlers["SetRelationshipStatusAsync"] = _ => Task.FromResult(true);
        await Service().SetRelationshipStatusAsync(PLAYER, FRIEND, 1, Ct);
        await Service().SetRelationshipStatusAsync(PLAYER, FRIEND, 0, Ct);

        _fakes.Handlers["SetRelationshipStatusAsync"] = _ => Task.FromResult(false);
        await Service().SetRelationshipStatusAsync(PLAYER, 9, 2, Ct);

        Counted().Should().Equal(RewardTrackActionTypes.SET_RELATIONSHIP_STATUS);
    }

    private List<object?> Counted() =>
        [.. _fakes.Log.Of("RecordActionAsync").Select(x => x.Args[0])];

    private MessengerService Service() =>
        new(
            _fakes.Create<IGrainFactory>(),
            _fakes.Create<IRoomService>(),
            NullLogger<IMessengerService>.Instance
        );
}
