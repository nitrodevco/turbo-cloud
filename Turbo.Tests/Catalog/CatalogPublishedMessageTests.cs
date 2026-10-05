using FluentAssertions;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Catalog;

/// <summary>
/// The message that tells a client the catalog changed, as the client reads it: whether to
/// refresh at once, then a new furnidata hash only when there is one. It was written empty, so
/// a client reading the boolean read past the end.
/// </summary>
public sealed class CatalogPublishedMessageTests
{
    [Fact]
    public void ItSaysToRefresh_AndNothingMore_WithoutAHash()
    {
        var packet = PacketHarness.Encode(new CatalogPublishedMessageComposer());

        packet.PopBoolean().Should().BeTrue();
        packet.Remaining.Should().Be(0);
    }

    [Fact]
    public void AHash_FollowsTheFlag()
    {
        var packet = PacketHarness.Encode(
            new CatalogPublishedMessageComposer
            {
                InstantlyRefreshCatalogue = false,
                NewFurniDataHash = "abc123",
            }
        );

        packet.PopBoolean().Should().BeFalse();
        packet.PopString().Should().Be("abc123");
        packet.Remaining.Should().Be(0);
    }
}
