using FluentAssertions;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Rooms;

/// <summary>
/// The room close as Habbo writes it: a short 2 and nothing more (official client capture, 37 of
/// 37 closes). It was written empty.
/// </summary>
public sealed class CloseConnectionMessageTests
{
    [Fact]
    public void ItCarriesAShortTwo_AndNothingMore()
    {
        var packet = PacketHarness.Encode(new CloseConnectionMessageComposer());

        packet.PopShort().Should().Be(2);
        packet.Remaining.Should().Be(0);
    }
}
