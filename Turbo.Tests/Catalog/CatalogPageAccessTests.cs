using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Turbo.Catalog;
using Turbo.Catalog.Exceptions;
using Turbo.Players.Configuration;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Primitives.Players.Wallet;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// A catalog page can ask for a rank (retro <c>min_rank</c>) or a permission node: a player
/// without it is sent an index without the page and what is under it, gets no answer for it, and
/// cannot buy its offers, while a player with it sees and buys as from any page. Leaving the page
/// out rather than sending it hidden is inferred: Habbo sends a hidden page with
/// <c>NodeData.visible</c> false (NodeData.as:24, e.g. princess_bc in the BUILDERS_CLUB index of
/// 2026-10-13T11-57-52-GetCatalogIndex) so a link still opens it by name, which a page the player
/// may not have must not allow; no recording shows a staff page, as the account is not staff.
/// </summary>
[Trait("evidence", "inferred")]
public sealed class CatalogPageAccessTests : IDisposable
{
    private const int STAFF = 30;
    private const int STAFF_TOOLS = 31;
    private const int EVENTS = 32;
    private const int STAFF_OFFER = 300;
    private const int EVENTS_OFFER = 301;
    private const string EVENTS_NODE = "group.events";

    private readonly CatalogFixture _catalog = new();
    private readonly List<WalletDebitRequest> _charged = [];
    private ResolvedPermissionsSnapshot _resolved = ResolvedPermissionsSnapshot.EMPTY;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CatalogPageAccessTests()
    {
        var staff = Page(STAFF, ROOT, "Staff", sort: 2);
        staff.MinRank = 7;
        var events = Page(EVENTS, FURNITURE, "Events", sort: 3);
        events.RequiredNode = EVENTS_NODE;

        _catalog.Db.Insert(staff);
        _catalog.Db.Insert(Page(STAFF_TOOLS, STAFF, "Staff Tools"));
        _catalog.Db.Insert(events);
        _catalog.AddOffer(STAFF_OFFER, STAFF_TOOLS, credits: 5);
        _catalog.AddOffer(EVENTS_OFFER, EVENTS, credits: 5);

        var registry = new PermissionRegistry([new CorePermissionNodeSource()]);

        _catalog.Fakes.Handlers["GetResolvedAsync"] = _ => Task.FromResult(_resolved);
        _catalog.Fakes.Handlers["get_Current"] = call =>
            call.Interface == typeof(IPermissionRegistryProvider) ? registry : Fakes.NotHandled;
    }

    public void Dispose() => _catalog.Dispose();

    /// <summary>Holding <c>navigator.category.staff</c> projects to security level 7.</summary>
    private void Grant(params string[] nodes) =>
        _resolved = ResolvedPermissionsSnapshot.EMPTY with { Granted = [.. nodes] };

    [Fact]
    public async Task A_page_above_the_players_rank_or_needing_a_node_they_lack_is_left_out_of_the_index_with_what_is_under_it()
    {
        var service = await ServiceAsync();

        (await IndexNamesAsync(service))
            .Should()
            .Contain("chairs")
            .And.NotContain(["staff", "staff tools", "events"]);

        Grant(PermissionNodes.Navigator.CATEGORY_STAFF);

        (await IndexNamesAsync(service))
            .Should()
            .Contain(["staff", "staff tools"])
            .And.NotContain("events", "the rank does not stand in for the node");

        Grant(EVENTS_NODE);

        (await IndexNamesAsync(service)).Should().Contain("events").And.NotContain("staff");
    }

    [Fact]
    public async Task A_page_the_player_may_not_have_is_not_answered_and_its_offers_are_not_sold()
    {
        var service = await ServiceAsync();

        (await PageAsync(service, STAFF_TOOLS)).Should().BeFalse();
        (await PageAsync(service, EVENTS)).Should().BeFalse();

        var buy = () => BuyAsync(service, STAFF_OFFER);

        (await buy.Should().ThrowAsync<CatalogPurchaseException>())
            .Which.ErrorType.Should()
            .Be(CatalogPurchaseErrorType.OfferNotFound);
        _charged.Should().BeEmpty();

        Grant(PermissionNodes.Navigator.CATEGORY_STAFF, EVENTS_NODE);

        (await PageAsync(service, STAFF_TOOLS)).Should().BeTrue();
        (await PageAsync(service, EVENTS)).Should().BeTrue();
        (await BuyAsync(service, STAFF_OFFER)).Id.Should().Be(STAFF_OFFER);
        (await BuyAsync(service, EVENTS_OFFER)).Id.Should().Be(EVENTS_OFFER);
        (await BuyAsync(service, SOLD)).Id.Should().Be(SOLD);
    }

    private async Task<CatalogService> ServiceAsync()
    {
        var normal = _catalog.NormalProvider();
        var buildersClub = _catalog.BuildersClubProvider();

        await normal.ReloadAsync(Ct);
        await buildersClub.ReloadAsync(Ct);

        return new CatalogService(
            NullLogger<ICatalogService>.Instance,
            normal,
            buildersClub,
            _catalog.Db,
            Options.Create(new PlayerConfig()),
            _catalog.Fakes.Create<IGrainFactory>(),
            _catalog.Fakes.Create<IPermissionRegistryProvider>()
        );
    }

    private static PacketHarness Harness(ICatalogService service)
    {
        var harness = new PacketHarness();

        harness.Resolver.Overrides[typeof(ICatalogService)] = service;

        return harness;
    }

    /// <summary>The page names of the index the client is sent, root first.</summary>
    private static async Task<List<string>> IndexNamesAsync(ICatalogService service)
    {
        var replies = await Harness(service)
            .SendAsync(
                PacketHarness.Incoming("GetCatalogIndexMessageEvent"),
                PacketHarness.Payload(w => w.String("NORMAL"))
            );
        var packet = replies.Single(x =>
            x.Header == PacketHarness.Outgoing("CatalogIndexMessageComposer")
        );
        var names = new List<string>();

        ReadNode(packet, names);

        return names;
    }

    /// <summary>One node as NodeData.as reads it, and its children.</summary>
    private static void ReadNode(ClientPacket packet, List<string> names)
    {
        packet.PopBoolean();
        packet.PopInt();
        packet.PopInt();
        names.Add(packet.PopString());
        packet.PopString();

        var offers = packet.PopInt();

        for (var i = 0; i < offers; i++)
            packet.PopInt();

        var children = packet.PopInt();

        for (var i = 0; i < children; i++)
            ReadNode(packet, names);
    }

    /// <summary>Whether asking for the page is answered with it.</summary>
    private static async Task<bool> PageAsync(ICatalogService service, int pageId)
    {
        var replies = await Harness(service)
            .SendAsync(
                PacketHarness.Incoming("GetCatalogPageMessageEvent"),
                PacketHarness.Payload(w => w.Int(pageId).Int(-1).String("NORMAL"))
            );

        return replies.Any(x => x.Header == PacketHarness.Outgoing("CatalogPageMessageComposer"));
    }

    /// <summary>Player 7's purchase grain buying an offer from the normal catalog.</summary>
    private async Task<CatalogOfferSnapshot> BuyAsync(CatalogService service, int offerId)
    {
        var fakes = _catalog.Fakes;

        fakes.Handlers["TryDebitAsync"] = call =>
        {
            _charged.AddRange((List<WalletDebitRequest>)call.Args[0]!);

            return Task.FromResult(WalletDebitResult.Success());
        };

        var grain = GrainHarness.Create(
            typeof(CatalogModule).Assembly,
            "Turbo.Catalog.Grains.CatalogPurchaseGrain",
            fakes,
            _catalog.Db,
            playerId: 7
        );

        Set(grain, "_catalogService", service);
        Set(grain, "_definitionProvider", _catalog.Definitions);
        typeof(Grain)
            .GetProperty(
                "GrainContext",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            )!
            .GetSetMethod(true)!
            .Invoke(
                grain,
                [
                    GrainContextStub.Create(
                        GrainId.Create(
                            GrainType.Create("catalogpurchase"),
                            GrainIdKeyExtensions.CreateIntegerKey(7)
                        )
                    ),
                ]
            );

        var purchase = await ((ICatalogPurchaseGrain)grain).PurchaseOfferFromCatalogAsync(
            CatalogType.Normal,
            offerId,
            string.Empty,
            1,
            Ct
        );

        return purchase.Offer;
    }

    private static void Set(object grain, string field, object value) =>
        grain
            .GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(grain, value);
}
