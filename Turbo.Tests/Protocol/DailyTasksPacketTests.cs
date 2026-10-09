using Turbo.Primitives.Messages.Outgoing.Quest;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Quests.Enums;
using Turbo.Primitives.Quests.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// The daily task packets in the order the AS3 reads them: <c>DailyTaskInfo</c> for the list
/// and the added tasks, the task update parser for 550, and <c>ClaimDailyTaskComposer</c>'s int.
/// </summary>
public class DailyTasksPacketTests
{
    [Fact]
    public void a_task_is_written_in_the_order_DailyTaskInfo_reads_it()
    {
        var reply = PacketHarness.Encode(
            new DailyTasksActiveListMessageComposer { Tasks = [Task(isBonus: false)] }
        );

        Assert.Equal(1, reply.PopInt());
        AssertTask(reply, isBonus: false);
        Assert.True(reply.End);
    }

    [Fact]
    public void added_tasks_are_written_the_same_way()
    {
        var reply = PacketHarness.Encode(
            new DailyTasksTasksAddedMessageComposer { Tasks = [Task(isBonus: true)] }
        );

        Assert.Equal(1, reply.PopInt());
        AssertTask(reply, isBonus: true);
        Assert.True(reply.End);
    }

    [Fact]
    public void an_update_is_task_id_repeats_status_and_seconds_left()
    {
        var reply = PacketHarness.Encode(
            new DailyTasksTaskUpdateMessageComposer
            {
                TaskId = 9,
                Repeats = 3,
                Status = DailyTaskStatus.Claimed,
                SecondsLeft = 60,
            }
        );

        Assert.Equal(9, reply.PopLong());
        Assert.Equal(3, reply.PopInt());
        Assert.Equal(2, reply.PopByte());
        Assert.Equal(60, reply.PopInt());
        Assert.True(reply.End);
    }

    [Fact]
    public async Task a_claim_hands_its_task_id_to_the_players_daily_tasks()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(
            PacketHarness.Incoming("ClaimDailyTaskMessageEvent"),
            PacketHarness.Payload(w => w.Int(9))
        );

        var claim = Assert.Single(harness.Fakes.Log.Of("ClaimAsync"));
        Assert.Equal(9L, claim.Args[0]);
    }

    [Fact]
    public async Task asking_for_the_tasks_asks_the_players_daily_tasks_to_send_them()
    {
        var harness = new PacketHarness();

        await harness.SendAsync(PacketHarness.Incoming("GetDailyTasksMessageEvent"), []);

        Assert.Single(harness.Fakes.Log.Of("SendTasksAsync"));
    }

    private static DailyTaskSnapshot Task(bool isBonus) =>
        new()
        {
            TaskId = 5_000_000_000,
            TaskCode = "1734011389107_G",
            QuestTypeCode = "explore",
            IsBonus = isBonus,
            ImageVersion = "v1",
            CatalogName = "cat",
            RequiredRepeats = 10,
            Repeats = 4,
            Status = DailyTaskStatus.Completed,
            SecondsLeft = 3600,
            Rewards =
            [
                new()
                {
                    ProductType = ProductDisplayType.ActivityPoints,
                    RewardTypeId = "0",
                    ExtraParams = "",
                    Amount = 10,
                },
            ],
        };

    private static void AssertTask(ClientPacket reply, bool isBonus)
    {
        Assert.Equal(5_000_000_000, reply.PopLong());
        Assert.Equal("1734011389107_G", reply.PopString());
        Assert.Equal("explore", reply.PopString());
        Assert.Equal(isBonus, reply.PopBoolean());
        Assert.Equal("v1", reply.PopString());
        Assert.Equal("cat", reply.PopString());
        Assert.Equal(10, reply.PopInt());
        Assert.Equal(4, reply.PopInt());
        Assert.Equal(1, reply.PopByte());
        Assert.Equal(3600, reply.PopInt());
        Assert.Equal(1, reply.PopInt());
        Assert.Equal(8, reply.PopShort());
        Assert.Equal("0", reply.PopString());
        Assert.Equal("", reply.PopString());
        Assert.Equal(10, reply.PopInt());
    }
}
