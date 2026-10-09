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
    public async Task opening_the_quest_window_is_answered_with_the_quests_the_server_has()
    {
        var harness = new PacketHarness();

        var replies = await harness.SendAsync(PacketHarness.Incoming("GetQuestsMessageEvent"), []);

        var reply = Assert.Single(replies);
        Assert.Equal(PacketHarness.Outgoing("QuestsMessageComposer"), reply.Header);
        Assert.Equal(0, reply.PopInt());
        Assert.True(reply.PopBoolean());
        Assert.True(reply.End);
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
