using FluentAssertions;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Protocol;

/// <summary>
/// The two mini mail messages as Habbo writes them. MiniMailNew carries the string "0", which
/// the client does not read (AS3 WIN63-202610151745
/// <c>MiniMailNewMessageMessageParser.parse</c>, no reads) but every official client capture of
/// it has; MiniMailUnreadCount is the count (<c>MiniMailUnreadCountMessageParser.parse</c>).
/// </summary>
public sealed class MiniMailPacketTests
{
    [Fact]
    public void Mini_mail_new_writes_the_string_zero_as_habbo_does()
    {
        var packet = PacketHarness.Encode(new MiniMailNewMessageComposer());

        PacketHarness
            .Revision.Serializers[typeof(MiniMailNewMessageComposer)]
            .Header.Should()
            .Be(PacketHarness.Outgoing("MiniMailNewMessageComposer"));
        packet.PopString().Should().Be("0");
        packet.Remaining.Should().Be(0);
    }

    [Fact]
    public void Mini_mail_unread_count_writes_the_count()
    {
        var packet = PacketHarness.Encode(
            new MiniMailUnreadCountMessageComposer { UnreadCount = 3 }
        );

        PacketHarness
            .Revision.Serializers[typeof(MiniMailUnreadCountMessageComposer)]
            .Header.Should()
            .Be(PacketHarness.Outgoing("MiniMailUnreadCountMessageComposer"));
        packet.PopInt().Should().Be(3);
        packet.Remaining.Should().Be(0);
    }
}
