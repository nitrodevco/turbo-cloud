using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Turbo.Catalog;
using Turbo.Catalog.Configuration;
using Turbo.Catalog.Reception;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Hotel;
using Turbo.Primitives.Hotel.Enums;
using Turbo.Primitives.Hotel.Snapshots;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Community goals: buying from a goal's catalog page and voting for a side count for it, and a
/// client asking reads where it stands as its <c>CommunityGoalProgressDataParser</c> does - the
/// levels the meter shows, the score to the next, its own score and rank - and its best
/// contributors as the hall of fame reads them.
/// </summary>
public sealed class CommunityGoalTests : IDisposable
{
    private const int BUYER = 7;
    private const int OTHER = 8;

    private static readonly DateTime NOW = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private readonly CatalogFixture _catalog = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(NOW));
    private readonly CommunityGoalService _goals;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CommunityGoalTests()
    {
        foreach (var (id, name) in new[] { (BUYER, "buyer"), (OTHER, "other") })
            _catalog.Db.Insert(
                new PlayerEntity
                {
                    Id = id,
                    Name = name,
                    Figure = $"hd-180-{id}",
                    Gender = AvatarGenderType.Male,
                    PlayerStatus = PlayerStatusType.Offline,
                }
            );

        _goals = new CommunityGoalService(
            _catalog.Db,
            Options.Create(new CatalogConfig()),
            _time,
            NullLogger<CommunityGoalService>.Instance
        );
    }

    public void Dispose() => _catalog.Dispose();

    [Fact]
    public async Task buying_from_the_goals_page_fills_its_meter_as_the_client_reads_it()
    {
        await _goals.SaveAsync(Goal("hotel_party", CommunityGoalMode.Normal, [2, 5, 10]), Ct);

        await BuyAsync(SOLD, quantity: 1);
        await BuyAsync(SOLD, quantity: 2);
        // Bought elsewhere: no help to the goal.
        await _goals.ContributeAsync(new PlayerId(OTHER), CHILD, 50, Ct);

        var read = await ProgressAsync(BUYER);

        read.Should()
            .Be(
                new Progress(
                    Expired: false,
                    PersonalScore: 3,
                    PersonalRank: 1,
                    Total: 3,
                    Level: 1,
                    Remaining: 2,
                    Percent: 33,
                    Code: "hotel_party",
                    SecondsLeft: (int)TimeSpan.FromDays(6).TotalSeconds,
                    RewardLimits: [1, 10]
                )
            );
        (await ProgressAsync(OTHER)).PersonalRank.Should().Be(0, "they gave nothing");
    }

    [Fact]
    public async Task a_vote_counts_once_and_the_needle_leans_to_the_side_ahead()
    {
        await _goals.SaveAsync(
            Goal("red_or_blue", CommunityGoalMode.VersusVote, [1, 3]) with
            {
                SideTwoPageId = CHILD,
            },
            Ct
        );

        (await VoteAsync(BUYER, 2)).Should().BeTrue();
        (await VoteAsync(BUYER, 1)).Should().BeFalse("a player votes once");
        await _goals.ContributeAsync(new PlayerId(OTHER), CHILD, 1, Ct);

        var read = await ProgressAsync(OTHER);

        read.Level.Should().Be(-1, "side two is two ahead, past the first level");
        read.Remaining.Should().Be(-1, "one more for side two reaches the next level");
        read.Percent.Should().Be(50);
        read.Total.Should().Be(2);

        var standing = await _goals.GetStandingAsync((await _goals.ListAsync(Ct))[0].Id, Ct);

        standing!.SideTwo.Should().Be(2);
        standing.VotesTwo.Should().Be(1);
    }

    [Fact]
    public async Task a_vote_for_a_goal_that_takes_none_or_is_over_is_not_counted()
    {
        await _goals.SaveAsync(Goal("plain", CommunityGoalMode.Normal, [5]), Ct);

        (await VoteAsync(BUYER, 1)).Should().BeFalse("the goal shown takes no votes");

        // Started after the first, so shown now, and already over.
        await _goals.SaveAsync(
            Goal("voting", CommunityGoalMode.VersusVote, [5]) with
            {
                StartsAt = NOW.AddHours(-2),
                EndsAt = NOW.AddHours(-1),
            },
            Ct
        );

        (await VoteAsync(BUYER, 1)).Should().BeFalse("the goal is over");

        var read = await ProgressAsync(BUYER);

        read.Code.Should().Be("voting");
        read.Expired.Should().BeTrue();
        read.SecondsLeft.Should().Be(0);
        read.Total.Should().Be(0);
    }

    [Fact]
    public async Task the_hall_of_fame_lists_the_best_contributors_best_first()
    {
        await _goals.SaveAsync(Goal("fame", CommunityGoalMode.Normal, [100]), Ct);
        await _goals.ContributeAsync(new PlayerId(BUYER), FURNITURE, 3, Ct);
        await _goals.ContributeAsync(new PlayerId(OTHER), FURNITURE, 9, Ct);

        var harness = Harness();
        var replies = await harness.SendAsync(
            PacketHarness.Incoming("GetCommunityGoalHallOfFameMessageEvent"),
            PacketHarness.Payload(w => w.String("fame")),
            playerId: BUYER
        );
        var packet = replies.Single(x =>
            x.Header == PacketHarness.Outgoing("CommunityGoalHallOfFameMessageComposer")
        );

        packet.PopString().Should().Be("fame");
        packet.PopInt().Should().Be(2);
        (packet.PopInt(), packet.PopString(), packet.PopString(), packet.PopInt(), packet.PopInt())
            .Should()
            .Be((OTHER, "other", "hd-180-8", 1, 9));
        (packet.PopInt(), packet.PopString(), packet.PopString(), packet.PopInt(), packet.PopInt())
            .Should()
            .Be((BUYER, "buyer", "hd-180-7", 2, 3));
        packet.Remaining.Should().Be(0);
    }

    [Fact]
    public async Task no_goal_started_sends_nothing()
    {
        await _goals.SaveAsync(
            Goal("later", CommunityGoalMode.Normal, [5]) with
            {
                StartsAt = NOW.AddDays(1),
                EndsAt = NOW.AddDays(2),
            },
            Ct
        );

        var replies = await Harness()
            .SendAsync(
                PacketHarness.Incoming("GetCommunityGoalProgressMessageEvent"),
                PacketHarness.Payload(_ => { }),
                playerId: BUYER
            );

        replies.Should().BeEmpty();
    }

    [Theory]
    [InlineData("has space", CommunityGoalMode.Normal, new[] { 5 }, null)]
    [InlineData("ok", CommunityGoalMode.Normal, new int[0], null)]
    [InlineData("ok", CommunityGoalMode.Normal, new[] { 5, 5 }, null)]
    [InlineData("ok", CommunityGoalMode.Normal, new[] { 1, 2, 3, 4 }, null)]
    [InlineData("ok", CommunityGoalMode.Normal, new[] { 5 }, CHILD)]
    public async Task a_goal_it_cant_play_is_refused(
        string code,
        CommunityGoalMode mode,
        int[] levels,
        int? sideTwo
    )
    {
        var save = () =>
            _goals.SaveAsync(Goal(code, mode, levels) with { SideTwoPageId = sideTwo }, Ct);

        await save.Should().ThrowAsync<ArgumentException>();
    }

    private static CommunityGoalSnapshot Goal(string code, CommunityGoalMode mode, int[] levels) =>
        new()
        {
            Id = 0,
            Code = code,
            Mode = mode,
            StartsAt = NOW.AddDays(-1),
            EndsAt = NOW.AddDays(6),
            LevelScores = [.. levels],
            RewardRanks = [1, 10],
            SideOnePageId = FURNITURE,
        };

    private PacketHarness Harness()
    {
        var harness = new PacketHarness();

        harness.Resolver.Overrides[typeof(ICommunityGoalService)] = _goals;

        return harness;
    }

    private async Task<bool> VoteAsync(int player, int side)
    {
        var replies = await Harness()
            .SendAsync(
                PacketHarness.Incoming("CommunityGoalVoteMessageEvent"),
                PacketHarness.Payload(w => w.Int(side)),
                playerId: player
            );

        return replies
            .Single(x => x.Header == PacketHarness.Outgoing("CommunityVoteReceivedMessageComposer"))
            .PopBoolean();
    }

    private async Task<Progress> ProgressAsync(int player)
    {
        var replies = await Harness()
            .SendAsync(
                PacketHarness.Incoming("GetCommunityGoalProgressMessageEvent"),
                PacketHarness.Payload(_ => { }),
                playerId: player
            );
        var packet = replies.Single(x =>
            x.Header == PacketHarness.Outgoing("CommunityGoalProgressMessageComposer")
        );
        var progress = new Progress(
            packet.PopBoolean(),
            packet.PopInt(),
            packet.PopInt(),
            packet.PopInt(),
            packet.PopInt(),
            packet.PopInt(),
            packet.PopInt(),
            packet.PopString(),
            packet.PopInt(),
            [.. Enumerable.Range(0, packet.PopInt()).Select(_ => packet.PopInt())]
        );

        packet.Remaining.Should().Be(0);

        return progress;
    }

    /// <summary>The buyer's purchase grain buying an offer, as <c>ProductParamPurchaseTests</c> does.</summary>
    private async Task BuyAsync(int offerId, int quantity)
    {
        var provider = _catalog.NormalProvider();

        await provider.ReloadAsync(Ct);

        var fakes = _catalog.Fakes;

        fakes.Handlers["GetCatalogSnapshot"] = _ => provider.Current;
        fakes.Handlers["TryDebitAsync"] = _ => Task.FromResult(WalletDebitResult.Success());

        var grain = GrainHarness.Create(
            typeof(CatalogModule).Assembly,
            "Turbo.Catalog.Grains.CatalogPurchaseGrain",
            fakes,
            _catalog.Db,
            playerId: BUYER
        );

        Set(grain, "_catalogService", fakes.Create<ICatalogService>());
        Set(grain, "_definitionProvider", _catalog.Definitions);
        Set(grain, "_communityGoals", _goals);
        typeof(Grain)
            .GetProperty(
                "GrainContext",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            )!
            .GetSetMethod(true)!
            .Invoke(
                grain,
                [
                    GrainContextStub.Create(
                        GrainId.Create(
                            GrainType.Create("catalogpurchase"),
                            GrainIdKeyExtensions.CreateIntegerKey(BUYER)
                        )
                    ),
                ]
            );

        await ((ICatalogPurchaseGrain)grain).PurchaseOfferFromCatalogAsync(
            CatalogType.Normal,
            offerId,
            string.Empty,
            quantity,
            Ct
        );
    }

    private static void Set(object grain, string field, object value) =>
        grain
            .GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(grain, value);

    private sealed record Progress(
        bool Expired,
        int PersonalScore,
        int PersonalRank,
        int Total,
        int Level,
        int Remaining,
        int Percent,
        string Code,
        int SecondsLeft,
        int[] RewardLimits
    )
    {
        public bool Equals(Progress? other) =>
            other is not null
            && (
                Expired,
                PersonalScore,
                PersonalRank,
                Total,
                Level,
                Remaining,
                Percent,
                Code,
                SecondsLeft
            )
                == (
                    other.Expired,
                    other.PersonalScore,
                    other.PersonalRank,
                    other.Total,
                    other.Level,
                    other.Remaining,
                    other.Percent,
                    other.Code,
                    other.SecondsLeft
                )
            && RewardLimits.SequenceEqual(other.RewardLimits);

        public override int GetHashCode() => HashCode.Combine(Code, Total);
    }
}
