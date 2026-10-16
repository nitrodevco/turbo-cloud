using FluentAssertions;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// <c>WiredMenuError</c> as Flash reads it (<c>WiredMenuErrorMessageParser.parse</c>:
/// <c>readShort</c>, WIN63-202610151745).
/// </summary>
public sealed class WiredMenuErrorTests
{
    [Fact]
    public void A_menu_error_carries_its_code_as_a_short()
    {
        var packet = PacketHarness.Encode(new WiredMenuErrorEventMessageComposer { ErrorCode = 7 });

        packet.Header.Should().Be(PacketHarness.Outgoing("WiredMenuErrorMessageComposer"));
        packet.PopShort().Should().Be(7);
        packet.Remaining.Should().Be(0);
    }
}
