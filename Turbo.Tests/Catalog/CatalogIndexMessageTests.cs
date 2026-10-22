using FluentAssertions;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// The catalog index as the client reads it. Habbo's root is page -1 named "root" with no title
/// or icon, and the Builders Club root holds one real tab, "furni", with the pages under it.
/// </summary>
public sealed class CatalogIndexMessageTests : IDisposable
{
    private readonly CatalogFixture _catalog = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _catalog.Dispose();

    private static (bool Visible, int Icon, int Id, string Name, string Title) Node(
        ClientPacket packet
    ) =>
        (
            packet.PopBoolean(),
            packet.PopInt(),
            packet.PopInt(),
            packet.PopString(),
            packet.PopString()
        );

    private static ClientPacket Index(CatalogSnapshot snapshot) =>
        PacketHarness.Encode(new CatalogIndexMessageComposer { Catalog = snapshot });

    [Fact]
    public async Task TheRoot_IsSentAsPageMinusOneNamedRoot_WhateverItsRowHolds()
    {
        var provider = _catalog.NormalProvider();

        await provider.ReloadAsync(Ct);

        var packet = Index(provider.Current);

        Node(packet).Should().Be((true, 0, -1, "root", ""));
        packet.PopInt().Should().Be(0, "the root has no offers");
        packet.PopInt().Should().Be(2, "Furniture and the hidden tab");
        Node(packet).Should().Be((true, 1, FURNITURE, "furniture", "Furniture"));
    }

    [Fact]
    public async Task TheBuildersClubRoot_HoldsTheRealTab_WithThePagesUnderIt()
    {
        var provider = _catalog.BuildersClubProvider();

        await provider.ReloadAsync(Ct);

        var packet = Index(provider.Current);

        Node(packet).Should().Be((true, 0, -1, "root", ""));
        packet.PopInt().Should().Be(0);
        packet.PopInt().Should().Be(1, "the client shows only the root's first child");
        Node(packet).Should().Be((true, 1, FURNITURE, "furniture", "Furniture"));
        packet.PopInt().Should().Be(0, "a tab sells nothing in this catalog");
        packet.PopInt().Should().Be(1);
        Node(packet).Should().Be((true, 1, BUILDERS_PAGE, "builders", "Builders"));
    }

    [Fact]
    public async Task AFolderOnlyPage_IsSentAsPageMinusOne_WithItsTitleAndThePagesUnderIt()
    {
        // Habbo's "Hot now" is page -1 under Furni: choosing it opens the pages under it and
        // loads no page of its own (CatalogNavigator.as:348).
        var hotNow = Page(20, FURNITURE, "Hot now", sort: 5);
        hotNow.FolderOnly = true;
        _catalog.Db.Insert(hotNow);
        _catalog.Db.Insert(Page(21, 20, "Wired"));
        var provider = _catalog.NormalProvider();

        await provider.ReloadAsync(Ct);

        var nodes = Tree(Index(provider.Current));

        nodes.Should().Contain(("Furniture", (true, 1, -1, "hot now", "Hot now")));
        nodes.Should().Contain(("Hot now", (true, 1, 21, "wired", "Wired")));
        nodes
            .Should()
            .Contain(
                ("Furniture", (true, 1, CHILD, "chairs", "Chairs")),
                "pages are sent as they were"
            );
    }

    /// <summary>Every node under the root, with the title of the node it is under.</summary>
    private static List<(string Parent, (bool, int, int, string, string) Node)> Tree(
        ClientPacket packet
    )
    {
        var nodes = new List<(string, (bool, int, int, string, string))>();

        Read(string.Empty);

        return nodes;

        void Read(string parent)
        {
            var node = Node(packet);

            nodes.Add((parent, node));

            var offers = packet.PopInt();

            for (var i = 0; i < offers; i++)
                packet.PopInt();

            var children = packet.PopInt();

            for (var i = 0; i < children; i++)
                Read(node.Item5);
        }
    }
}
