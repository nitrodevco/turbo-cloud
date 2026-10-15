using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Quests.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// The quest window asks with <c>GetQuests</c> when the player opens it. The answer is read by
/// the client's <c>QuestsMessageParser</c>: a count of <c>QuestMessageData</c>, then whether to
/// open the window.
/// </summary>
public class QuestsPacketTests
{
    [Fact]
    public async Task opening_the_quest_window_asks_the_players_quests_to_open_it()
    {
        var harness = new PacketHarness();

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("GetQuestsMessageEvent"),
            [],
            playerId: 3
        );

        Assert.Empty(replies);
        var call = Assert.Single(harness.Fakes.Log.Of("SendQuestsAsync"));
        Assert.Equal(3L, Convert.ToInt64(call.Key));
        Assert.Equal(true, call.Args[0]);
    }

    [Fact]
    public async Task the_seasonal_calendar_asks_the_players_seasonal_quests()
    {
        var harness = new PacketHarness();

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("GetSeasonalQuestsOnlyMessageEvent"),
            [],
            playerId: 3
        );

        Assert.Empty(replies);
        var call = Assert.Single(harness.Fakes.Log.Of("SendSeasonalQuestsAsync"));
        Assert.Equal(3L, Convert.ToInt64(call.Key));
    }

    /// <summary>
    /// The official SeasonalQuests (official client capture 2026-10-12, session 102507 at
    /// 5719466 ms): two seasonal quests and nothing after them.
    /// </summary>
    [Fact]
    public void seasonal_quests_are_the_quests_with_no_open_window_flag_as_recorded()
    {
        var packet = PacketHarness.Encode(
            new SeasonalQuestsMessageComposer
            {
                Quests =
                [
                    Recorded("bawcollab_worlds", 10, 2094, "1771579447385", "1771579236591"),
                    Recorded("bawcollab_dlc", 3, 2095, "1771583870994", "1771583727360"),
                ],
            }
        );

        Assert.Equal(
            "000000020010626177636F6C6C61625F776F726C6473000000000000000AFFFFFFFF0000082E00000A574541525F4241444745000000000000000D313737313537393434373338350000000000000001000000010000000D31373731353739323336353931010100C52634000D626177636F6C6C61625F646C630000000000000003FFFFFFFF0000082F00000A574541525F4241444745000000000000000D313737313538333837303939340000000000000001000000010000000D31373731353833373237333630010100C52634",
            Convert.ToHexString(packet.PopBytes(packet.Remaining))
        );
    }

    private static QuestSnapshot Recorded(
        string campaign,
        int count,
        int id,
        string localization,
        string chain
    ) =>
        new()
        {
            CampaignCode = campaign,
            CompletedQuestsInCampaign = 0,
            QuestCountInCampaign = count,
            ActivityPointType = -1,
            Id = id,
            Accepted = false,
            Type = "WEAR_BADGE",
            ImageVersion = "",
            RewardCurrencyAmount = 0,
            LocalizationCode = localization,
            CompletedSteps = 0,
            TotalSteps = 1,
            SortOrder = 1,
            CatalogPageName = "",
            ChainCode = chain,
            Easy = true,
            IsSeasonal = true,
            SecondsLeft = 12920372,
        };

    [Fact]
    public async Task the_daily_quest_asks_for_the_easy_or_hard_quest_at_an_index()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            PacketHarness.Incoming("GetDailyQuestMessageEvent"),
            PacketHarness.Payload(w => w.Bool(false).Int(2)),
            playerId: 3
        );

        var call = Assert.Single(harness.Fakes.Log.Of("SendDailyQuestAsync"));
        Assert.Equal(false, call.Args[0]);
        Assert.Equal(2, call.Args[1]);
    }

    [Fact]
    public void the_daily_quest_is_a_flag_then_the_quest_and_the_two_counts()
    {
        var none = PacketHarness.Encode(new QuestDailyMessageComposer { Quest = null });
        Assert.False(none.PopBoolean());
        Assert.True(none.End);

        var daily = PacketHarness.Encode(
            new QuestDailyMessageComposer
            {
                Quest = Quest(isSeasonal: false),
                EasyQuestCount = 3,
                HardQuestCount = 4,
            }
        );
        Assert.True(daily.PopBoolean());
        AssertQuest(daily, id: 7, isSeasonal: false);
        Assert.Equal(3, daily.PopInt());
        Assert.Equal(4, daily.PopInt());
        Assert.True(daily.End);
    }

    [Fact]
    public async Task starting_a_campaign_names_it()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            PacketHarness.Incoming("StartCampaignMessageEvent"),
            PacketHarness.Payload(w => w.String("room_builder")),
            playerId: 3
        );

        var call = Assert.Single(harness.Fakes.Log.Of("StartCampaignAsync"));
        Assert.Equal("room_builder", call.Args[0]);
    }

    [Fact]
    public async Task accepting_activating_and_rejecting_name_the_quest()
    {
        var harness = new PacketHarness();

        foreach (var name in new[] { "AcceptQuest", "ActivateQuest", "RejectQuest" })
            await harness.SendAsync(
                PacketHarness.Incoming($"{name}MessageEvent"),
                PacketHarness.Payload(w => w.Int(42)),
                playerId: 3
            );

        Assert.Equal(
            [42, 42],
            harness.Fakes.Log.Of("AcceptAsync").Select(x => (int)x.Args[0]!).ToArray()
        );
        Assert.Equal(42, (int)Assert.Single(harness.Fakes.Log.Of("RejectAsync")).Args[0]!);
    }

    [Fact]
    public void cancelled_and_completed_wrap_the_quest_as_their_parsers_read_it()
    {
        var cancelled = PacketHarness.Encode(
            new QuestCancelledMessageComposer { Expired = true, Quest = Quest(isSeasonal: false) }
        );
        Assert.True(cancelled.PopBoolean());
        AssertQuest(cancelled, id: 7, isSeasonal: false);
        Assert.True(cancelled.End);

        var completed = PacketHarness.Encode(
            new QuestCompletedMessageComposer { Quest = Quest(isSeasonal: true), ShowDialog = true }
        );
        AssertQuest(completed, id: 7, isSeasonal: true);
        Assert.True(completed.PopBoolean());
        Assert.True(completed.End);

        var quest = PacketHarness.Encode(new QuestMessageComposer { Quest = Quest(false) });
        AssertQuest(quest, id: 7, isSeasonal: false);
        Assert.True(quest.End);
    }

    [Fact]
    public void a_quest_is_written_in_the_order_QuestMessageData_reads_it()
    {
        var reply = PacketHarness.Encode(
            new QuestsMessageComposer
            {
                Quests = [Quest(isSeasonal: false), Quest(isSeasonal: true) with { Id = 8 }],
                OpenWindow = false,
            }
        );

        Assert.Equal(2, reply.PopInt());
        AssertQuest(reply, id: 7, isSeasonal: false);
        AssertQuest(reply, id: 8, isSeasonal: true);
        Assert.False(reply.PopBoolean());
        Assert.True(reply.End);
    }

    private static QuestSnapshot Quest(bool isSeasonal) =>
        new()
        {
            CampaignCode = "explore",
            CompletedQuestsInCampaign = 1,
            QuestCountInCampaign = 5,
            ActivityPointType = 0,
            Id = 7,
            Accepted = true,
            Type = "ENTER_OTHERS_ROOM",
            ImageVersion = "v2",
            RewardCurrencyAmount = 10,
            LocalizationCode = "visit_room",
            CompletedSteps = 2,
            TotalSteps = 3,
            SortOrder = 4,
            CatalogPageName = "explore_page",
            ChainCode = "chain",
            Easy = true,
            IsSeasonal = isSeasonal,
            SecondsLeft = 600,
        };

    private static void AssertQuest(
        Turbo.Primitives.Packets.ClientPacket reply,
        int id,
        bool isSeasonal
    )
    {
        Assert.Equal("explore", reply.PopString());
        Assert.Equal(1, reply.PopInt());
        Assert.Equal(5, reply.PopInt());
        Assert.Equal(0, reply.PopInt());
        Assert.Equal(id, reply.PopInt());
        Assert.True(reply.PopBoolean());
        Assert.Equal("ENTER_OTHERS_ROOM", reply.PopString());
        Assert.Equal("v2", reply.PopString());
        Assert.Equal(10, reply.PopInt());
        Assert.Equal("visit_room", reply.PopString());
        Assert.Equal(2, reply.PopInt());
        Assert.Equal(3, reply.PopInt());
        Assert.Equal(4, reply.PopInt());
        Assert.Equal("explore_page", reply.PopString());
        Assert.Equal("chain", reply.PopString());
        Assert.True(reply.PopBoolean());
        Assert.Equal(isSeasonal, reply.PopBoolean());

        if (isSeasonal)
            Assert.Equal(600, reply.PopInt());
    }
}
