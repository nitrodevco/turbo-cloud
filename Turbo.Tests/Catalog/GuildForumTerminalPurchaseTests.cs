using System.Reflection;
using FluentAssertions;
using Orleans;
using Orleans.Runtime;
using Turbo.Catalog;
using Turbo.Catalog.Editing;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Grains;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Wallet;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// "In order to start a group forum the group owner must first purchase a forum terminal for the
/// group" (group.forum.description): buying a guild_forum furni for a group asks that group's
/// forum to open, naming the buyer, which the forum grain checks is the owner.
/// </summary>
public sealed class GuildForumTerminalPurchaseTests : IDisposable
{
    private const int TERMINAL = 50;
    private const int GROUP = 9;
    private const int BUYER = 7;

    private readonly CatalogFixture _catalog = new();
    private readonly CatalogEditService _service;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public GuildForumTerminalPurchaseTests()
    {
        _service = new CatalogEditService(
            _catalog.Db,
            _catalog.Definitions,
            _catalog.NormalProvider(),
            _catalog.BuildersClubProvider(),
            _catalog.Fakes.Create<ISessionGateway>(),
            _catalog.Fakes.Create<IGrainFactory>(),
            new CapturingLogger<ICatalogEditService>()
        );
        _catalog.AddDefinition(
            TERMINAL,
            "guild_forum",
            set: x => x.Logic = GuildFurnitureLogicNames.FORUM
        );
        _catalog.Fakes.Handlers["GetMemberRankAsync"] = _ =>
            Task.FromResult<GuildMemberRank?>(GuildMemberRank.Owner);
    }

    public void Dispose() => _catalog.Dispose();

    [Fact]
    public async Task Buying_a_forum_terminal_for_a_group_asks_its_forum_to_open()
    {
        var offer = await OfferAsync(TERMINAL);

        await BuyAsync(offer, GROUP.ToString());

        var open = _catalog.Fakes.Log.Of("OpenAsync").Should().ContainSingle().Subject;
        open.Interface.Should().Be<IGuildForumGrain>();
        open.Key.Should().Be((long)GROUP);
        open.Args[0].Should().Be((PlayerId)BUYER);
    }

    [Fact]
    public async Task Other_furni_opens_no_forum()
    {
        var offer = await OfferAsync(CHAIR);

        await BuyAsync(offer, "");

        _catalog.Fakes.Log.Of("OpenAsync").Should().BeEmpty();
    }

    private async Task<int> OfferAsync(int definitionId) =>
        (
            await _service.CreateOfferAsync(
                (PlayerId)1,
                new CatalogOfferDraft(
                    FURNITURE,
                    string.Empty,
                    10,
                    0,
                    null,
                    true,
                    true,
                    0,
                    true,
                    new CatalogProductDraft(ProductType.Floor, definitionId, null, 1)
                ),
                Ct
            )
        ).Id;

    private async Task BuyAsync(int offerId, string extraParam)
    {
        var provider = _catalog.NormalProvider();

        await provider.ReloadAsync(Ct);

        var fakes = _catalog.Fakes;

        fakes.Handlers["GetCatalogSnapshot"] = _ => provider.Current;
        fakes.Handlers["TryDebitAsync"] = _ => Task.FromResult(WalletDebitResult.Success());

        var grain = GrainHarness.Create(
            typeof(CatalogModule).Assembly,
            "Turbo.Catalog.Grains.CatalogPurchaseGrain",
            fakes,
            _catalog.Db,
            playerId: BUYER
        );

        Set(grain, "_catalogService", fakes.Create<ICatalogService>());
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
                            GrainIdKeyExtensions.CreateIntegerKey(BUYER)
                        )
                    ),
                ]
            );

        await ((ICatalogPurchaseGrain)grain).PurchaseOfferFromCatalogAsync(
            CatalogType.Normal,
            offerId,
            extraParam,
            1,
            Ct
        );
    }

    private static void Set(object grain, string field, object value) =>
        grain
            .GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(grain, value);
}
