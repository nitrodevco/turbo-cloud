using System.Globalization;
using EvalHarness;
using Xunit;

namespace EvalHidden;

/// <summary>
/// Hidden regression tests for the reception's timing requests, stated only in wire bytes:
/// the client's packet goes through the revision parser and the registered handler, and what
/// comes back is decoded from the revision serializer's output.
/// </summary>
public class ReceptionTimingTests
{
    private static string Utc(DateTime t) => t.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static async Task<(string Echo, string Code)> TimingCode(string schedule)
    {
        var h = new PacketHarness();
        var header = PacketHarness.Incoming("GetCurrentTimingCodeMessageEvent");
        var replies = await h.SendAsync(header, PacketHarness.Payload(w => w.String(schedule)));
        var reply = Assert.Single(replies);
        Assert.Equal(PacketHarness.Outgoing("CurrentTimingCodeMessageComposer"), reply.Header);
        var echo = reply.PopString();
        var code = reply.PopString();
        Assert.True(reply.End, "reply carries exactly the schedule and the code");
        return (echo, code);
    }

    private static async Task<(string Echo, int Seconds)> SecondsUntil(string time)
    {
        var h = new PacketHarness();
        var header = PacketHarness.Incoming("GetSecondsUntilMessageEvent");
        var replies = await h.SendAsync(header, PacketHarness.Payload(w => w.String(time)));
        var reply = Assert.Single(replies);
        Assert.Equal(PacketHarness.Outgoing("SecondsUntilMessageComposer"), reply.Header);
        var echo = reply.PopString();
        var seconds = reply.PopInt();
        Assert.True(reply.End, "reply carries exactly the time string and the seconds");
        return (echo, seconds);
    }

    [Fact]
    public async Task TimingCode_IsLatestStartedEntry_AndEchoesSchedule()
    {
        var schedule = "2000-01-01 00:00,alpha;2001-06-01 12:30,beta;2999-01-01 00:00,omega";
        var (echo, code) = await TimingCode(schedule);
        Assert.Equal(schedule, echo);
        Assert.Equal("beta", code);
    }

    [Fact]
    public async Task TimingCode_OrderOfEntriesDoesNotMatter()
    {
        var schedule = "2001-06-01 12:30,beta;2999-01-01 00:00,omega;2000-01-01 00:00,alpha";
        var (_, code) = await TimingCode(schedule);
        Assert.Equal("beta", code);
    }

    [Fact]
    public async Task TimingCode_UsesUtcNowAgainstMinutePrecisionTimes()
    {
        var past = Utc(DateTime.UtcNow.AddMinutes(-3));
        var future = Utc(DateTime.UtcNow.AddMinutes(10));
        var schedule = $"2000-01-01 00:00,old;{past},now;{future},later";
        var (_, code) = await TimingCode(schedule);
        Assert.Equal("now", code);
    }

    [Fact]
    public async Task TimingCode_NothingStartedYet_IsEmpty()
    {
        var (echo, code) = await TimingCode("2999-01-01 00:00,future");
        Assert.Equal("2999-01-01 00:00,future", echo);
        Assert.Equal(string.Empty, code);
    }

    [Fact]
    public async Task TimingCode_MalformedEntriesAreSkipped()
    {
        var (_, code) = await TimingCode("garbage;2000-01-01 00:00,alpha;;not a date,x");
        Assert.Equal("alpha", code);
    }

    [Fact]
    public async Task SecondsUntil_FutureTime_CountsDownInUtc()
    {
        var target = DateTime.UtcNow.AddHours(2);
        target = new DateTime(target.Year, target.Month, target.Day, target.Hour, target.Minute, 0, DateTimeKind.Utc);
        var text = Utc(target);
        var expected = (target - DateTime.UtcNow).TotalSeconds;
        var (echo, seconds) = await SecondsUntil(text);
        Assert.Equal(text, echo);
        Assert.InRange(seconds, expected - 5, expected + 5);
    }

    [Fact]
    public async Task SecondsUntil_PastTime_IsZero()
    {
        var (echo, seconds) = await SecondsUntil("2000-01-01 00:00");
        Assert.Equal("2000-01-01 00:00", echo);
        Assert.Equal(0, seconds);
    }

    [Fact]
    public async Task SecondsUntil_Unparseable_IsZero_AndEchoed()
    {
        var (echo, seconds) = await SecondsUntil("not-a-time");
        Assert.Equal("not-a-time", echo);
        Assert.Equal(0, seconds);
    }
}
