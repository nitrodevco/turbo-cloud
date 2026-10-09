using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Quests.Enums;
using Turbo.Primitives.Quests.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// The reward track packets in the order the AS3 parsers read them (quest/rewardtrack): the
/// tracks (2614), progress (844), a claim's result (3381), a premium purchase's result (917),
/// and the two requests, a claim (track id, prize id) and a purchase (track id).
/// </summary>
public class RewardTracksPacketTests
{
    [Fact]
    public void a_track_with_premium_is_written_in_the_parsers_order()
    {
        var reply = PacketHarness.Encode(
            new RewardTracksMessageComposer
            {
                Disabled = false,
                Tracks =
                [
                    Track(
                        new()
                        {
                            TaskPointsBoost = 1.5,
                            InstantPoints = 100,
                            CostDiamonds = 20,
                            CostCredits = 10,
                        }
                    ),
                ],
                Reload = true,
            }
        );

        Assert.False(reply.PopBoolean());
        Assert.Equal(1, reply.PopInt());
        Assert.Equal("introduction", reply.PopString());
        Assert.Equal("cyan", reply.PopString());
        Assert.Equal(370, reply.PopInt());
        Assert.True(reply.PopBoolean());
        Assert.Equal(1.5, BitConverter.Int64BitsToDouble(reply.PopLong()));
        Assert.Equal(100, reply.PopInt());
        Assert.Equal(20, reply.PopInt());
        Assert.Equal(10, reply.PopInt());
        AssertRest(reply);
        Assert.True(reply.PopBoolean());
        Assert.True(reply.End);
    }

    [Fact]
    public void a_track_without_premium_leaves_its_premium_fields_out()
    {
        var reply = PacketHarness.Encode(
            new RewardTracksMessageComposer
            {
                Disabled = false,
                Tracks = [Track(null)],
                Reload = false,
            }
        );

        reply.PopBoolean();
        reply.PopInt();
        reply.PopString();
        reply.PopString();
        reply.PopInt();
        Assert.False(reply.PopBoolean());
        AssertRest(reply);
        Assert.False(reply.PopBoolean());
        Assert.True(reply.End);
    }

    [Fact]
    public void progress_and_results_are_written_in_the_parsers_order()
    {
        var progress = PacketHarness.Encode(
            new RewardTrackProgressMessageComposer
            {
                TrackId = "introduction",
                TaskId = "visit_rooms",
                ProgressCount = 20,
                Points = 370,
            }
        );
        Assert.Equal("introduction", progress.PopString());
        Assert.Equal("visit_rooms", progress.PopString());
        Assert.Equal(20, progress.PopInt());
        Assert.Equal(370, progress.PopInt());
        Assert.True(progress.End);

        var claim = PacketHarness.Encode(
            new RewardTrackClaimResultMessageComposer
            {
                TrackId = "introduction",
                PrizeId = "p1",
                Result = RewardTrackClaimResult.PremiumRequired,
            }
        );
        Assert.Equal("introduction", claim.PopString());
        Assert.Equal("p1", claim.PopString());
        Assert.Equal(8, claim.PopInt());
        Assert.True(claim.End);

        var premium = PacketHarness.Encode(
            new RewardTrackPremiumPurchaseResultMessageComposer
            {
                TrackId = "introduction",
                Result = RewardTrackPremiumResult.NotEnoughDiamonds,
                Points = 5,
            }
        );
        Assert.Equal("introduction", premium.PopString());
        Assert.Equal(8, premium.PopInt());
        Assert.Equal(5, premium.PopInt());
        Assert.True(premium.End);
    }

    [Fact]
    public async Task a_claim_and_a_purchase_reach_the_players_reward_tracks()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            PacketHarness.Incoming("ClaimRewardTrackPrizeMessageEvent"),
            PacketHarness.Payload(w => w.String("introduction").String("p1"))
        );
        await harness.SendAsync(
            PacketHarness.Incoming("PurchaseRewardTrackPremiumMessageEvent"),
            PacketHarness.Payload(w => w.String("introduction"))
        );

        var claim = Assert.Single(harness.Fakes.Log.Of("ClaimPrizeAsync"));
        Assert.Equal(["introduction", "p1"], claim.Args[..2]);
        Assert.Equal(
            "introduction",
            Assert.Single(harness.Fakes.Log.Of("PurchasePremiumAsync")).Args[0]
        );
    }

    private static RewardTrackSnapshot Track(RewardTrackPremiumSnapshot? premium) =>
        new()
        {
            Id = "introduction",
            Theme = "cyan",
            Points = 370,
            PremiumConfig = premium,
            Premium = true,
            Complete = false,
            PremiumComplete = true,
            Tasks =
            [
                new()
                {
                    Id = "visit_rooms",
                    ActionType = "enter_other_users_room",
                    Parameter = "",
                    ProgressCount = 20,
                    Premium = false,
                    Levels =
                    [
                        new()
                        {
                            RequiredCount = 20,
                            PointsReward = 30,
                            Premium = true,
                        },
                    ],
                },
            ],
            Prizes =
            [
                new()
                {
                    Id = "p1",
                    RequiredPoints = 300,
                    ProductType = ProductDisplayType.Badge,
                    RewardTypeId = "RT_INTRO",
                    ExtraParams = "",
                    Amount = 1,
                    Premium = true,
                    Available = true,
                    Claimed = false,
                },
            ],
        };

    /// <summary>From owned premium to the end of the prizes.</summary>
    private static void AssertRest(ClientPacket reply)
    {
        Assert.True(reply.PopBoolean());
        Assert.False(reply.PopBoolean());
        Assert.True(reply.PopBoolean());
        Assert.Equal(1, reply.PopInt());
        Assert.Equal("visit_rooms", reply.PopString());
        Assert.Equal("enter_other_users_room", reply.PopString());
        Assert.Equal("", reply.PopString());
        Assert.Equal(20, reply.PopInt());
        Assert.False(reply.PopBoolean());
        Assert.Equal(1, reply.PopInt());
        Assert.Equal(20, reply.PopInt());
        Assert.Equal(30, reply.PopInt());
        Assert.True(reply.PopBoolean());
        Assert.Equal(1, reply.PopInt());
        Assert.Equal("p1", reply.PopString());
        Assert.Equal(300, reply.PopInt());
        Assert.Equal(4, reply.PopShort());
        Assert.Equal("RT_INTRO", reply.PopString());
        Assert.Equal("", reply.PopString());
        Assert.Equal(1, reply.PopInt());
        Assert.True(reply.PopBoolean());
        Assert.True(reply.PopBoolean());
        Assert.False(reply.PopBoolean());
    }
}
