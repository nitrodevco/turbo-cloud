using FluentAssertions;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Catalog;
using Turbo.Admin.Configuration;
using Turbo.Catalog.Editing;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// The catalog front page's featured items: the editor replaces them as a list, placed in its
/// order, each checked for what the client can draw and open; they go live when the catalog is
/// published, and only a front page is sent them, without the ones that have ended and with a
/// countdown on the ones that will.
/// </summary>
public sealed class CatalogFeaturedItemsTests : IDisposable
{
    private static readonly PlayerId Editor = 1;
    private static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    private readonly CatalogFixture _catalog = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(Now));
    private readonly CatalogEditService _service;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CatalogFeaturedItemsTests()
    {
        _service = new CatalogEditService(
            _catalog.Db,
            _catalog.Definitions,
            _catalog.NormalProvider(),
            _catalog.BuildersClubProvider(),
            _catalog.Fakes.Create<ISessionGateway>(),
            _catalog.Fakes.Create<IGrainFactory>(),
            new CapturingLogger<ICatalogEditService>(),
            time: _time
        );
    }

    public void Dispose() => _catalog.Dispose();

    private AdminCatalogQueries Queries() =>
        new(_catalog.Db, _catalog.Definitions, _service, Options.Create(new AdminConfig()));

    private static CatalogFeaturedItemDraft Item(
        CatalogFrontPageItemType type = CatalogFrontPageItemType.Page,
        string value = "furniture",
        string title = "New furni",
        string image = "catalogue/feature_cata_vert_furni.png",
        DateTime? ends = null
    ) => new(title, image, type, value, ends);

    [Fact]
    public async Task SavingTheItems_ReplacesThemAll_InTheOrderGiven()
    {
        var saved = await _service.SaveFeaturedItemsAsync(
            Editor,
            [
                Item(title: "  Big one  ", value: " furniture "),
                Item(CatalogFrontPageItemType.Offer, $"{SOLD}", ends: Now.AddDays(1)),
                Item(CatalogFrontPageItemType.Product, "hc_membership_code", image: ""),
            ],
            Ct
        );

        saved.Saved.Should().BeTrue(saved.Error);
        saved.Id.Should().Be(0);
        _service.UnpublishedChanges.Should().Be(1);

        var items = (await Queries().GetFeaturedAsync(Ct)).Items;

        items
            .Select(x => (x.Position, x.Title, x.Type, x.Value))
            .Should()
            .Equal(
                (1, "Big one", "page", "furniture"),
                (2, "New furni", "offer", $"{SOLD}"),
                (3, "New furni", "product", "hc_membership_code")
            );
        items[1].ExpiresAtUtc.Should().Be(Now.AddDays(1));
        items[0].ExpiresAtUtc.Should().BeNull();

        (await _service.SaveFeaturedItemsAsync(Editor, [Item(value: "chairs")], Ct))
            .Saved.Should()
            .BeTrue();
        (await Queries().GetFeaturedAsync(Ct))
            .Items.Should()
            .ContainSingle()
            .Which.Should()
            .Match<Turbo.Admin.Api.Contracts.CatalogFeaturedItem>(x =>
                x.Position == 1 && x.Value == "chairs"
            );

        (await _service.SaveFeaturedItemsAsync(Editor, [], Ct)).Saved.Should().BeTrue();
        (await Queries().GetFeaturedAsync(Ct)).Items.Should().BeEmpty();
    }

    public static TheoryData<CatalogFeaturedItemDraft, string> Refusals =>
        new()
        {
            { Item(title: " "), "title" },
            { Item(title: new string('a', 101)), "title" },
            { Item(image: new string('a', 256)), "picture" },
            { Item(value: " "), "opens" },
            { Item(value: "two words"), "name" },
            { Item(CatalogFrontPageItemType.Offer, "abc"), "its id" },
            { Item(CatalogFrontPageItemType.Offer, "0"), "its id" },
            { Item(CatalogFrontPageItemType.Offer, "999"), "no offer 999" },
            { Item(CatalogFrontPageItemType.Product, new string('a', 101)), "product code" },
            { Item(ends: Now.AddMinutes(-1)), "future" },
        };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task AnItemTheClientCouldNotDrawOrOpen_IsRefused(
        CatalogFeaturedItemDraft item,
        string why
    )
    {
        var result = await _service.SaveFeaturedItemsAsync(Editor, [Item(), item], Ct);

        result.Saved.Should().BeFalse();
        result.Error.Should().Contain(why);
        (await Queries().GetFeaturedAsync(Ct)).Items.Should().BeEmpty("nothing is half saved");
    }

    [Fact]
    public async Task TheFrontPageShowsFour_AndNoMore()
    {
        (await _service.SaveFeaturedItemsAsync(Editor, [.. Enumerable.Repeat(Item(), 5)], Ct))
            .Error.Should()
            .Contain("4");
    }

    [Fact]
    public async Task AFrontPage_IsSentThePublishedItems_AndOtherPagesNone()
    {
        var front = await _service.CreatePageAsync(
            Editor,
            ROOT,
            new("Front", "frontpage", 1, "frontpage4", [], [], CatalogPageDisplay.Regular),
            Ct
        );
        var featured = await _service.CreatePageAsync(
            Editor,
            ROOT,
            new("Featured", null, 1, "frontpage_featured", [], [], CatalogPageDisplay.Regular),
            Ct
        );

        (
            await _service.SaveFeaturedItemsAsync(
                Editor,
                [
                    Item(value: "furniture"),
                    Item(CatalogFrontPageItemType.Offer, $"{SOLD}", ends: Now.AddHours(1)),
                    Item(CatalogFrontPageItemType.Product, "some_code"),
                ],
                Ct
            )
        )
            .Saved.Should()
            .BeTrue();
        // One that has ended by the time a player looks is not sent.
        _catalog.Db.Insert(
            new CatalogFeaturedItemEntity
            {
                Position = 4,
                Title = "Gone",
                Image = string.Empty,
                Type = CatalogFrontPageItemType.Page,
                Value = "gone",
                ExpiresAt = Now.AddMinutes(30),
            }
        );

        var provider = _catalog.NormalProvider();

        await provider.ReloadAsync(Ct);
        _time.Advance(TimeSpan.FromMinutes(45));

        var page = await CatalogPagePackets.RequestAsync(provider.Current, front.Id, _time);

        page.Layout.Should().Be("frontpage4");
        page.FrontPageItems.Should()
            .Equal(
                new CatalogPagePackets.FrontPageItem(
                    1,
                    "New furni",
                    "catalogue/feature_cata_vert_furni.png",
                    0,
                    "furniture",
                    0
                ),
                new CatalogPagePackets.FrontPageItem(
                    2,
                    "New furni",
                    "catalogue/feature_cata_vert_furni.png",
                    1,
                    $"{SOLD}",
                    15 * 60
                ),
                new CatalogPagePackets.FrontPageItem(
                    3,
                    "New furni",
                    "catalogue/feature_cata_vert_furni.png",
                    2,
                    "some_code",
                    0
                )
            );
        (await CatalogPagePackets.RequestAsync(provider.Current, featured.Id, _time))
            .FrontPageItems.Should()
            .HaveCount(3);
        (await CatalogPagePackets.RequestAsync(provider.Current, FURNITURE, _time))
            .FrontPageItems.Should()
            .BeEmpty("a default_3x3 page draws no featured items");
    }

    [Fact]
    public async Task SavedItems_AreNotSentUntilPublished()
    {
        var front = await _service.CreatePageAsync(
            Editor,
            ROOT,
            new("Front", "frontpage", 1, "frontpage4", [], [], CatalogPageDisplay.Regular),
            Ct
        );
        var provider = _catalog.NormalProvider();

        _catalog.Fakes.Handlers["GetOnlinePlayerIds"] = _ => (IReadOnlyCollection<PlayerId>)[];

        var service = new CatalogEditService(
            _catalog.Db,
            _catalog.Definitions,
            provider,
            _catalog.BuildersClubProvider(),
            _catalog.Fakes.Create<ISessionGateway>(),
            _catalog.Fakes.Create<IGrainFactory>(),
            new CapturingLogger<ICatalogEditService>(),
            time: _time
        );

        await provider.ReloadAsync(Ct);
        (await service.SaveFeaturedItemsAsync(Editor, [Item()], Ct)).Saved.Should().BeTrue();

        (await CatalogPagePackets.RequestAsync(provider.Current, front.Id, _time))
            .FrontPageItems.Should()
            .BeEmpty();

        await service.PublishAsync(Editor, Ct);

        (await CatalogPagePackets.RequestAsync(provider.Current, front.Id, _time))
            .FrontPageItems.Should()
            .ContainSingle()
            .Which.Value.Should()
            .Be("furniture");
    }
}
