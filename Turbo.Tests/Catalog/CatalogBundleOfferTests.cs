using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Turbo.Admin.Catalog;
using Turbo.Admin.Configuration;
using Turbo.Catalog;
using Turbo.Catalog.Editing;
using Turbo.Catalog.Exceptions;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Wallet;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// An offer that gives several things, or a bot or a pet: the editor takes a list of products
/// that replaces what the offer gave, checks each against how a purchase hands it out, and the
/// editor and the client get back what was saved. A badge in it is given when it is bought.
/// </summary>
public sealed class CatalogBundleOfferTests : IDisposable
{
    private const int MISSING = 999;

    private static readonly PlayerId Editor = 1;

    private readonly CatalogFixture _catalog = new();
    private readonly CatalogEditService _service;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CatalogBundleOfferTests()
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
    }

    public void Dispose() => _catalog.Dispose();

    private static CatalogOfferDraft Gives(params CatalogProductDraft[] products) =>
        new(FURNITURE, string.Empty, 10, 0, null, true, true, 0, true, null, null, products);

    private static CatalogProductDraft Floor(int quantity = 1) =>
        new(ProductType.Floor, CHAIR, null, quantity);

    private static CatalogProductDraft Badge(string code = "ADM") =>
        new(ProductType.Badge, null, code, 1);

    private static CatalogProductDraft Effect(string id = "108", int copies = 1) =>
        new(ProductType.Effect, null, id, copies);

    private static CatalogProductDraft Bot(string? figure = "hd-180-1", int? item = null) =>
        new(ProductType.Robot, item, figure, 1);

    private static CatalogProductDraft Pet(string? type = "3", int? item = null) =>
        new(ProductType.Pet, item, type, 1);

    private async Task<Turbo.Admin.Api.Contracts.CatalogOfferItem> EditorOfferAsync(int offerId)
    {
        var page = await new AdminCatalogQueries(
            _catalog.Db,
            _catalog.Definitions,
            _service,
            Options.Create(new AdminConfig())
        ).GetPageAsync(FURNITURE, Ct);

        return page!.Offers.Single(x => x.Id == offerId);
    }

    private async Task<CatalogPagePackets.Offer> ClientOfferAsync(int offerId)
    {
        var provider = _catalog.NormalProvider();

        await provider.ReloadAsync(Ct);

        return (await CatalogPagePackets.RequestAsync(provider.Current, FURNITURE)).Offers.Single(
            x => x.Id == offerId
        );
    }

    [Fact]
    public async Task ABundleOfFurniABadgeAndAnEffect_IsSaved_AndComesBackAsSaved()
    {
        var result = await _service.CreateOfferAsync(
            Editor,
            Gives(Floor(2), Badge(), Effect(copies: 3)),
            Ct
        );

        result.Saved.Should().BeTrue(result.Error);

        var saved = await EditorOfferAsync(result.Id);

        saved
            .Products.Select(x => (x.Type, x.DefinitionId, x.ExtraParam, x.Quantity))
            .Should()
            .Equal(
                ("floor", (int?)CHAIR, (string?)null, 2),
                ("badge", null, "ADM", 1),
                ("effect", null, "108", 3)
            );
        saved.LocalizationId.Should().Be("chair", "named by its first product");
        saved.CanBundle.Should().BeFalse("a badge is given once, however many are bought");

        (await ClientOfferAsync(result.Id))
            .Products.Should()
            .Equal(
                new CatalogPagePackets.Product("s", CHAIR, string.Empty, 2),
                new CatalogPagePackets.Product("b", 0, "ADM", 1),
                new CatalogPagePackets.Product("e", 108, "108", 3)
            );
    }

    [Fact]
    public async Task AListOfProducts_ReplacesWhatTheOfferGave()
    {
        var result = await _service.UpdateOfferAsync(
            Editor,
            SOLD,
            Gives(Badge("XYZ"), new(ProductType.Wall, POSTER, null, 1)),
            Ct
        );

        result.Saved.Should().BeTrue(result.Error);
        (await EditorOfferAsync(SOLD))
            .Products.Select(x => (x.Type, x.ExtraParam ?? x.DefinitionName))
            .Should()
            .Equal(("badge", "XYZ"), ("wall", "poster"));

        (await _service.UpdateOfferAsync(Editor, SOLD, Gives(Floor()), Ct)).Saved.Should().BeTrue();
        (await EditorOfferAsync(SOLD))
            .Products.Should()
            .ContainSingle()
            .Which.Type.Should()
            .Be("floor");
    }

    [Fact]
    public async Task APetOffer_IsNamedForThePetPage_AndSentAsAPet()
    {
        var byType = await _service.CreateOfferAsync(Editor, Gives(Pet("3")), Ct);
        var byItem = await _service.CreateOfferAsync(Editor, Gives(Pet(null, PET_ITEM)), Ct);

        byType.Saved.Should().BeTrue(byType.Error);
        byItem.Saved.Should().BeTrue(byItem.Error);
        (await EditorOfferAsync(byType.Id)).LocalizationId.Should().Be("a0 pet3");
        (await EditorOfferAsync(byItem.Id)).LocalizationId.Should().Be("pet5");
        (await EditorOfferAsync(byItem.Id))
            .Products.Single()
            .DefinitionId.Should()
            .Be(PET_ITEM, "the pet's type is read from its item");

        var sent = await ClientOfferAsync(byType.Id);

        sent.Products.Should().ContainSingle().Which.Type.Should().Be("p");
        sent.CanBundle.Should().BeFalse();
    }

    [Fact]
    public async Task ABotOffer_WearsItsFigure_AndIsNamedByItsItemOrAsABot()
    {
        var plain = await _service.CreateOfferAsync(Editor, Gives(Bot()), Ct);
        var named = await _service.CreateOfferAsync(Editor, Gives(Bot(item: CHAIR)), Ct);

        plain.Saved.Should().BeTrue(plain.Error);
        named.Saved.Should().BeTrue(named.Error);
        (await EditorOfferAsync(plain.Id)).LocalizationId.Should().Be("bot");
        (await EditorOfferAsync(named.Id)).LocalizationId.Should().Be("chair");
        (await EditorOfferAsync(named.Id)).Products.Single().DefinitionId.Should().Be(CHAIR);

        var sent = await ClientOfferAsync(plain.Id);

        sent.Products.Should().Equal(new CatalogPagePackets.Product("r", -1, "hd-180-1", 1));
    }

    public static TheoryData<CatalogProductDraft[], string> Refusals =>
        new()
        {
            { [Pet(null)], "pet's type" },
            { [Pet("cat")], "pet's type" },
            { [Pet("1"), Pet("2")], "one pet" },
            { [Bot(null)], "figure" },
            { [Bot(" ")], "figure" },
            { [Bot(item: MISSING)], "gone" },
            {
                [
                    Floor(),
                    new(ProductType.HabboClub, null, null, 1, SubscriptionType.HabboClub, 31),
                ],
                "on its own"
            },
            { [], "1 to 20" },
            { [.. Enumerable.Repeat(Floor(), 21)], "1 to 20" },
            { [Badge() with { Quantity = 2 }], "one at a time" },
            { [Floor(), Badge(" ")], "badge code" },
            { [Floor(0)], "1 to 100" },
        };

    [Theory]
    [MemberData(nameof(Refusals))]
    public async Task WhatThePurchaseCouldNotHandOut_IsRefused(
        CatalogProductDraft[] products,
        string why
    )
    {
        var result = await _service.CreateOfferAsync(Editor, Gives(products), Ct);

        result.Saved.Should().BeFalse();
        result.Error.Should().Contain(why);
        _service.UnpublishedChanges.Should().Be(0);
    }

    [Fact]
    public async Task ALimitedSeries_KeepsItsOneItem_WhateverTheListSays()
    {
        _catalog.Db.Insert(
            new LtdSeriesEntity
            {
                Id = 1,
                CatalogProductEntityId = SOLD,
                TotalQuantity = 10,
                RemainingQuantity = 10,
                RaffleWindowSeconds = 0,
                IsActive = true,
            }
        );

        (await _service.UpdateOfferAsync(Editor, SOLD, Gives(Floor(), Badge()), Ct))
            .Error.Should()
            .Contain("limited series");
        (await _service.UpdateOfferAsync(Editor, SOLD, Gives(Badge()), Ct))
            .Error.Should()
            .Contain("limited series");
        (await _service.UpdateOfferAsync(Editor, SOLD, Gives(Floor()), Ct))
            .Saved.Should()
            .BeTrue("the same item, saved again");
    }

    [Fact]
    public async Task AClubGiftsList_IsFurniOnly()
    {
        (
            await _service.CreateOfferAsync(
                Editor,
                Gives(Floor(), Badge()) with
                {
                    LocalizationId = "gift",
                    ClubGiftDaysRequired = 31,
                },
                Ct
            )
        )
            .Error.Should()
            .Contain("gives furni");
    }

    [Fact]
    public async Task BuyingABundle_GivesItsBadge()
    {
        var offer = await _service.CreateOfferAsync(Editor, Gives(Floor(), Badge("ADM")), Ct);

        await BuyAsync(offer.Id);

        _catalog.Fakes.Log.Of("GiveBadgeAsync").Select(x => x.Args[0]).Should().Equal("ADM");
    }

    [Fact]
    public async Task ABadgeTheBuyerHas_IsNotSoldAgain()
    {
        var offer = await _service.CreateOfferAsync(Editor, Gives(Floor(), Badge("ADM")), Ct);

        _catalog.Fakes.Handlers["HasBadgeAsync"] = _ => Task.FromResult(true);

        var buy = () => BuyAsync(offer.Id);

        (await buy.Should().ThrowAsync<CatalogPurchaseException>())
            .Which.ErrorType.Should()
            .Be(CatalogPurchaseErrorType.BadgeOwned);
        _catalog.Fakes.Log.Of("TryDebitAsync").Should().BeEmpty("nothing is charged");
        _catalog.Fakes.Log.Of("GiveBadgeAsync").Should().BeEmpty();
    }

    /// <summary>The buyer's purchase grain buying an offer from the catalog as published.</summary>
    private async Task BuyAsync(int offerId)
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
            playerId: 7
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
                            GrainIdKeyExtensions.CreateIntegerKey(7)
                        )
                    ),
                ]
            );

        await ((ICatalogPurchaseGrain)grain).PurchaseOfferFromCatalogAsync(
            CatalogType.Normal,
            offerId,
            string.Empty,
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
