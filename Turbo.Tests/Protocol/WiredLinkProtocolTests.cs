using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// Room Linker link requests in wire bytes. The live hotel answers each of them, the link
/// window's configuration request included, with WiredLinkOperationResult code 7 "This Link is
/// no longer available in the room" and no configuration or network (official client capture
/// 2026-10-18, 5 of 5: the configuration request as operation 0, a create as 2, a save as 1),
/// in the layout of the AS3 result parser (<c>wiredlink._-X29</c>).
/// </summary>
public class WiredLinkProtocolTests
{
    private const int ROOM = 80463631;
    private const int LINKER = 924611506;

    public static TheoryData<string, int, byte[]> Requests() =>
        new()
        {
            {
                "WiredLinkGetConfigurationMessageEvent",
                0,
                PacketHarness.Payload(w => w.Int(LINKER))
            },
            {
                "WiredLinkSaveConfigurationMessageEvent",
                1,
                PacketHarness.Payload(w => w.Int(LINKER).String("0").Bool(false))
            },
            {
                "WiredLinkCreateNetworkMessageEvent",
                2,
                PacketHarness.Payload(w => w.Int(LINKER).String("lab"))
            },
            {
                "WiredLinkRenameNetworkMessageEvent",
                3,
                PacketHarness.Payload(w => w.Int(LINKER).String("5").String("lab"))
            },
            {
                "WiredLinkDeleteNetworkMessageEvent",
                4,
                PacketHarness.Payload(w => w.Int(LINKER).String("5"))
            },
            {
                "WiredLinkGetConnectionsMessageEvent",
                5,
                PacketHarness.Payload(w => w.Int(LINKER).String("5"))
            },
        };

    [Theory]
    [MemberData(nameof(Requests))]
    public async Task Every_link_request_is_answered_that_the_link_is_unavailable(
        string request,
        int operation,
        byte[] payload
    )
    {
        var harness = new PacketHarness();

        var replies = await harness.SendAsync(
            PacketHarness.Incoming(request),
            payload,
            roomId: ROOM
        );

        var reply = Assert.Single(replies);
        Assert.Equal(
            PacketHarness.Outgoing("WiredLinkOperationResultMessageComposer"),
            reply.Header
        );
        Assert.Equal(ROOM, reply.PopInt());
        Assert.Equal(LINKER, reply.PopInt());
        Assert.Equal(operation, reply.PopInt());
        Assert.Equal(7, reply.PopInt());
        Assert.False(reply.PopBoolean());
        Assert.Equal("0", reply.PopString());
        Assert.Equal(0, reply.PopInt());
        Assert.True(reply.End);
    }

    [Fact]
    public async Task Outside_a_room_a_link_request_gets_no_answer()
    {
        var replies = await new PacketHarness().SendAsync(
            PacketHarness.Incoming("WiredLinkGetConfigurationMessageEvent"),
            PacketHarness.Payload(w => w.Int(LINKER))
        );

        Assert.Empty(replies);
    }
}
