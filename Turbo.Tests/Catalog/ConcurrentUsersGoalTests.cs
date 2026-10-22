using System.Collections.Immutable;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Catalog.Configuration;
using Turbo.Catalog.Reception;
using Turbo.Primitives.Hotel;
using Turbo.Primitives.Hotel.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Catalog;

/// <summary>
/// The reception's concurrent users goal (<c>ConcurrentUsersInfoElementHandler</c>): it asks for
/// the progress every five seconds until the goal is reached, and a click asks for the reward and
/// the progress again. Its states are 0 disabled, 1 active, 2 a reward to take, 3 taken.
/// </summary>
public sealed class ConcurrentUsersGoalTests
{
    private const int PLAYER = 1;
    private const string BADGE = "CU2026";

    private readonly Fakes _fakes = new();
    private readonly HashSet<string> _badges = [];
    private int _online;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ConcurrentUsersGoalTests()
    {
        _fakes.Handlers[nameof(ISessionGateway.GetOnlinePlayerIds)] = _ =>
            Enumerable.Range(1, _online).Select(x => (PlayerId)x).ToList();
        _fakes.Handlers["HasBadgeAsync"] = call =>
            Task.FromResult(_badges.Contains((string)call.Args[0]!));
        _fakes.Handlers["GiveBadgeAsync"] = call =>
            Task.FromResult(_badges.Add((string)call.Args[0]!));
    }

    [Fact]
    public async Task No_goal_set_is_disabled()
    {
        _online = 4;

        var progress = await Service(goal: 0).GetProgressAsync(PLAYER, Ct);

        (progress.State, progress.UserCount, progress.UserCountGoal)
            .Should()
            .Be((ConcurrentUsersGoalState.Disabled, 4, 0));
    }

    [Fact]
    public async Task System_accounts_online_are_not_counted()
    {
        _online = 4;
        _fakes.Handlers["GetSystemAccountIdsAsync"] = _ =>
            Task.FromResult(ImmutableArray.Create<PlayerId>(2, 3, 9));

        var progress = await Service(goal: 3).GetProgressAsync(PLAYER, Ct);

        (progress.State, progress.UserCount).Should().Be((ConcurrentUsersGoalState.Active, 2));
    }

    [Fact]
    public async Task Short_of_the_goal_is_active_and_its_reward_is_refused()
    {
        _online = 2;
        var service = Service(goal: 3);

        await service.ClaimRewardAsync(PLAYER, Ct);
        var progress = await service.GetProgressAsync(PLAYER, Ct);

        (progress.State, progress.UserCount, progress.UserCountGoal)
            .Should()
            .Be((ConcurrentUsersGoalState.Active, 2, 3));
        _badges.Should().BeEmpty();
    }

    [Fact]
    public async Task A_goal_reached_stays_reached_and_its_badge_is_taken_once()
    {
        var service = Service(goal: 3);
        _online = 3;
        (await service.GetProgressAsync(PLAYER, Ct))
            .State.Should()
            .Be(ConcurrentUsersGoalState.Redeem);

        _online = 1;
        (await service.GetProgressAsync(PLAYER, Ct))
            .State.Should()
            .Be(ConcurrentUsersGoalState.Redeem);

        await service.ClaimRewardAsync(PLAYER, Ct);
        await service.ClaimRewardAsync(PLAYER, Ct);

        (await service.GetProgressAsync(PLAYER, Ct))
            .State.Should()
            .Be(ConcurrentUsersGoalState.Rewarded);
        _fakes.Log.Of("GiveBadgeAsync").Should().ContainSingle().Which.Args[0].Should().Be(BADGE);
    }

    [Fact]
    public async Task A_goal_with_no_reward_badge_is_rewarded_once_reached()
    {
        _online = 3;

        var progress = await Service(goal: 3, badge: "").GetProgressAsync(PLAYER, Ct);

        progress.State.Should().Be(ConcurrentUsersGoalState.Rewarded);
    }

    [Fact]
    public async Task The_progress_is_the_state_the_count_and_the_goal()
    {
        _online = 5;
        var harness = new PacketHarness();
        harness.Resolver.Overrides[typeof(IConcurrentUsersGoalService)] = Service(goal: 8);

        var reply = (
            await harness.SendAsync(
                PacketHarness.Incoming("GetConcurrentUsersGoalProgressMessageEvent"),
                [],
                playerId: PLAYER
            )
        ).Single();

        reply
            .Header.Should()
            .Be(PacketHarness.Outgoing("ConcurrentUsersGoalProgressMessageComposer"));
        (reply.PopInt(), reply.PopInt(), reply.PopInt()).Should().Be((1, 5, 8));
        reply.End.Should().BeTrue();
    }

    [Fact]
    public async Task The_reward_click_gives_the_badge()
    {
        _online = 8;
        var harness = new PacketHarness();
        harness.Resolver.Overrides[typeof(IConcurrentUsersGoalService)] = Service(goal: 8);

        await harness.SendAsync(
            PacketHarness.Incoming("GetConcurrentUsersRewardMessageEvent"),
            [],
            playerId: PLAYER
        );

        _badges.Should().Equal(BADGE);
    }

    private ConcurrentUsersGoalService Service(int goal, string badge = BADGE) =>
        new(
            Options.Create(
                new CatalogConfig
                {
                    Reception = new ReceptionConfig
                    {
                        ConcurrentUsersGoal = goal,
                        ConcurrentUsersRewardBadge = badge,
                    },
                }
            ),
            _fakes.Create<IGrainFactory>(),
            _fakes.Create<ISessionGateway>(),
            NullLogger<ConcurrentUsersGoalService>.Instance
        );
}
