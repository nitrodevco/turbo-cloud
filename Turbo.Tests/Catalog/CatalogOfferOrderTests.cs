using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Catalog;
using Turbo.Admin.Configuration;
using Turbo.Catalog.Editing;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// The order of a page's offers is the editor's: moving an offer puts it where it was dropped,
/// on its own page or another, and the editor, the published page and the client all list the
/// offers in that order. Moving does not take an offer anywhere saving it there could not.
/// </summary>
public sealed class CatalogOfferOrderTests : IDisposable
{
    private static readonly PlayerId Editor = 1;

    private readonly CatalogFixture _catalog = new();
    private readonly CatalogEditService _service;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CatalogOfferOrderTests()
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

    private async Task<int[]> EditorOrderAsync(int pageId)
    {
        var page = await new AdminCatalogQueries(
            _catalog.Db,
            _catalog.Definitions,
            _service,
            Options.Create(new AdminConfig())
        ).GetPageAsync(pageId, Ct);

        return [.. page!.Offers.Select(x => x.Id)];
    }

    private async Task<int[]> ClientOrderAsync(int pageId)
    {
        var provider = _catalog.NormalProvider();

        await provider.ReloadAsync(Ct);

        var page = await CatalogPagePackets.RequestAsync(provider.Current, pageId);

        return [.. page.Offers.Select(x => x.Id)];
    }

    private static CatalogOfferDraft Draft(int pageId, CatalogProductDraft? product = null) =>
        new(
            pageId,
            string.Empty,
            10,
            0,
            null,
            true,
            true,
            0,
            true,
            product ?? new CatalogProductDraft(ProductType.Floor, CHAIR, null, 1)
        );

    [Fact]
    public async Task MovingAnOffer_ReordersItsPage_ForTheEditorAndTheClient()
    {
        var moved = await _service.MoveOfferAsync(Editor, IN_DIAMONDS, FURNITURE, 0, Ct);

        moved.Saved.Should().BeTrue(moved.Error);
        moved.Id.Should().Be(IN_DIAMONDS);
        _service.UnpublishedChanges.Should().Be(1);
        (await EditorOrderAsync(FURNITURE))
            .Should()
            .Equal(IN_DIAMONDS, SOLD, HIDDEN_OFFER, IN_DUCKETS, IN_CREDITS_ROW);
        (await ClientOrderAsync(FURNITURE))
            .Should()
            .Equal(
                [IN_DIAMONDS, SOLD, IN_DUCKETS, IN_CREDITS_ROW],
                "a hidden offer is listed nowhere"
            );

        // Past the end is the end.
        (await _service.MoveOfferAsync(Editor, SOLD, FURNITURE, 99, Ct))
            .Saved.Should()
            .BeTrue();
        (await EditorOrderAsync(FURNITURE))
            .Should()
            .Equal(IN_DIAMONDS, HIDDEN_OFFER, IN_DUCKETS, IN_CREDITS_ROW, SOLD);
    }

    [Fact]
    public async Task AnOfferMovedToAnotherPage_GoesWhereAsked_AndTheOldPageClosesUp()
    {
        _catalog.AddOffer(200, CHILD);
        _catalog.AddOffer(201, CHILD);

        (await _service.MoveOfferAsync(Editor, SOLD, CHILD, 1, Ct)).Saved.Should().BeTrue();

        (await EditorOrderAsync(CHILD)).Should().Equal(200, SOLD, 201);
        (await ClientOrderAsync(CHILD)).Should().Equal(200, SOLD, 201);
        (await EditorOrderAsync(FURNITURE))
            .Should()
            .Equal(HIDDEN_OFFER, IN_DUCKETS, IN_DIAMONDS, IN_CREDITS_ROW);

        await using var db = await _catalog.Db.CreateDbContextAsync(Ct);

        (
            await db
                .CatalogOffers.Where(x => x.CatalogPageEntityId == FURNITURE)
                .OrderBy(x => x.Id)
                .Select(x => x.SortOrder)
                .ToListAsync(Ct)
        )
            .Should()
            .Equal(0, 1, 2, 3);
    }

    [Fact]
    public async Task ANewOffer_AndOneSavedOntoAnotherPage_GoLast()
    {
        (await _service.MoveOfferAsync(Editor, IN_CREDITS_ROW, FURNITURE, 0, Ct))
            .Saved.Should()
            .BeTrue();

        var created = await _service.CreateOfferAsync(Editor, Draft(FURNITURE), Ct);

        created.Saved.Should().BeTrue(created.Error);
        (await EditorOrderAsync(FURNITURE)).Should().EndWith(created.Id);

        _catalog.AddOffer(200, CHILD);
        _catalog.AddOffer(201, CHILD);
        (await _service.UpdateOfferAsync(Editor, IN_CREDITS_ROW, Draft(CHILD), Ct))
            .Saved.Should()
            .BeTrue();

        (await EditorOrderAsync(CHILD)).Should().Equal(200, 201, IN_CREDITS_ROW);
    }

    [Fact]
    public async Task WhatOnlyTheNormalCatalogSells_IsNotMovedOutOfIt()
    {
        var membership = await _service.CreateOfferAsync(
            Editor,
            Draft(
                CHILD,
                new CatalogProductDraft(
                    ProductType.HabboClub,
                    null,
                    null,
                    1,
                    SubscriptionType.HabboClub,
                    31
                )
            ),
            Ct
        );

        (await _service.MoveOfferAsync(Editor, membership.Id, BUILDERS_PAGE, 0, Ct))
            .Error.Should()
            .Contain("Memberships are sold from the normal catalog");

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

        (await _service.MoveOfferAsync(Editor, SOLD, BUILDERS_PAGE, 0, Ct))
            .Error.Should()
            .Contain("Limited series are sold from the normal catalog");
        (
            await _service.UpdateOfferAsync(
                Editor,
                SOLD,
                Draft(BUILDERS_PAGE) with
                {
                    Product = null,
                },
                Ct
            )
        )
            .Error.Should()
            .Contain("Limited series are sold from the normal catalog");
        (await _service.MoveOfferAsync(Editor, SOLD, 999, 0, Ct)).Error.Should().Contain("gone");
        (await _service.MoveOfferAsync(Editor, 999, FURNITURE, 0, Ct))
            .Error.Should()
            .Contain("gone");
        (await _service.MoveOfferAsync(Editor, IN_DUCKETS, BUILDERS_PAGE, 0, Ct))
            .Saved.Should()
            .BeTrue("an offer of furni is sold from either catalog");
    }
}
