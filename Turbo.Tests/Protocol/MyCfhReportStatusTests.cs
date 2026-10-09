using Turbo.Primitives.Messages.Outgoing.Callforhelp;
using Turbo.Primitives.Moderation.Enums;
using Turbo.Primitives.Moderation.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// The help window's "my reports" asks <c>GetCfhMyReportStatus</c> and reads a count of reports,
/// each as the client's <c>SanctionStatusMessageParser</c> reads one: id, created, message,
/// topic, reported name, closed, sanctioned, by auto moderation, appeal status (a byte), appeal
/// created and appeal resolved, the ids and times as longs.
/// </summary>
public class MyCfhReportStatusTests
{
    [Fact]
    public async Task asking_for_my_reports_is_answered_with_the_reports_the_server_has()
    {
        var harness = new PacketHarness();

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("GetCfhMyReportStatusMessageEvent"),
            []
        );

        var reply = Assert.Single(replies);
        Assert.Equal(PacketHarness.Outgoing("MyCfhReportStatusMessageComposer"), reply.Header);
        Assert.Equal(0, reply.PopInt());
        Assert.True(reply.End);
    }

    [Fact]
    public void a_report_is_written_in_the_order_the_client_reads_it()
    {
        var reply = PacketHarness.Encode(
            new MyCfhReportStatusMessageComposer
            {
                Reports =
                [
                    new CfhReportStatusSnapshot
                    {
                        Id = 5_000_000_000,
                        CreatedAtMs = 1_760_000_000_000,
                        Message = "spam",
                        TopicId = 7,
                        ReportedName = "bob",
                        ClosedAtMs = -1,
                        Sanctioned = true,
                        SanctionedByAutoModeration = false,
                        AppealStatus = CfhAppealStatusType.DecidedWithoutAction,
                        AppealCreatedAtMs = 1_760_000_100_000,
                        AppealResolvedAtMs = -1,
                    },
                ],
            }
        );

        Assert.Equal(1, reply.PopInt());
        Assert.Equal(5_000_000_000, reply.PopLong());
        Assert.Equal(1_760_000_000_000, reply.PopLong());
        Assert.Equal("spam", reply.PopString());
        Assert.Equal(7, reply.PopInt());
        Assert.Equal("bob", reply.PopString());
        Assert.Equal(-1, reply.PopLong());
        Assert.True(reply.PopBoolean());
        Assert.False(reply.PopBoolean());
        Assert.Equal(3, reply.PopByte());
        Assert.Equal(1_760_000_100_000, reply.PopLong());
        Assert.Equal(-1, reply.PopLong());
        Assert.True(reply.End);
    }
}
