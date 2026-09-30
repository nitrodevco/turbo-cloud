using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// A packet end to end, stated in wire bytes: the revision's parser, the registered handler and
/// the revision's serializer. <see cref="PacketHarness"/> does not care what the message or
/// composer properties are called, only what the client sends and receives.
/// </summary>
public class LatencyPingTests
{
    [Fact]
    public async Task PingRequest_IsAnsweredWithTheSameRequestId()
    {
        var harness = new PacketHarness();

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("LatencyPingRequestMessageEvent"),
            PacketHarness.Payload(w => w.Int(42))
        );

        var reply = Assert.Single(replies);
        Assert.Equal(PacketHarness.Outgoing("LatencyPingResponseMessageComposer"), reply.Header);
        Assert.Equal(42, reply.PopInt());
        Assert.True(reply.End);
    }
}
