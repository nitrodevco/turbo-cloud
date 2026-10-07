using FluentAssertions;
using Orleans;
using Turbo.Catalog.Editing;
using Turbo.Database.Entities.Catalog;
using Turbo.Database.Extensions;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Editing;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Inventory;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// A catalog product that sells an avatar effect: the editor takes it, the client is sent its
/// effect id where it expects one, and what a purchase would give is worked out the same way
/// before the buyer is charged as when it is delivered.
/// </summary>
public sealed class EffectProductTests : IDisposable
{
    private const int OFFER_ID = 500;

    private static readonly PlayerId Editor = 1;

    private readonly CatalogFixture _catalog = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _catalog.Dispose();

    private CatalogEditService NewEditService() =>
        new(
            _catalog.Db,
            _catalog.Definitions,
            _catalog.NormalProvider(),
            _catalog.BuildersClubProvider(),
            _catalog.Fakes.Create<ISessionGateway>(),
            _catalog.Fakes.Create<IGrainFactory>(),
            new CapturingLogger<ICatalogEditService>()
        );

    private static CatalogOfferDraft Draft(CatalogProductDraft product) =>
        new(FURNITURE, string.Empty, 10, 0, null, true, true, 0, true, product);

    private static CatalogProductSnapshot Product(
        ProductType type,
        string? extraParam,
        int quantity,
        int id = 1
    ) =>
        new()
        {
            Id = id,
            OfferId = OFFER_ID,
            ProductType = type,
            FurniDefinitionId = -1,
            SpriteId = -1,
            ExtraParam = extraParam,
            Quantity = quantity,
            UniqueSize = 0,
            UniqueRemaining = 0,
            ClassName = null,
        };

    [Fact]
    public async Task TheEditorTakesAnEffectProductWithAnIdAndACopyCount()
    {
        var result = await NewEditService()
            .CreateOfferAsync(Editor, Draft(new(ProductType.Effect, null, "108", 3)), Ct);

        result.Saved.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("seven")]
    [InlineData("7.5")]
    public async Task TheEditorRefusesAnEffectProductThatNamesNoEffect(string? extraParam)
    {
        var result = await NewEditService()
            .CreateOfferAsync(Editor, Draft(new(ProductType.Effect, null, extraParam, 1)), Ct);

        result.Saved.Should().BeFalse();
        result.Error.Should().Contain("effect id");
    }

    [Fact]
    public async Task TheEditorRefusesAnEffectProductThatGivesNoCopies()
    {
        var result = await NewEditService()
            .CreateOfferAsync(Editor, Draft(new(ProductType.Effect, null, "7", 0)), Ct);

        result.Saved.Should().BeFalse();
        result.Error.Should().Contain("1 to 100");
    }

    [Fact]
    public void TheClientIsSentTheEffectIdWhereItExpectsASpriteId()
    {
        var snapshot = new CatalogProductEntity
        {
            ProductType = ProductType.Effect,
            ExtraParam = "108",
            Quantity = 2,
            CatalogOfferEntityId = OFFER_ID,
            Offer = null!,
        }.ToSnapshot(null, null);

        snapshot.SpriteId.Should().Be(108);
        snapshot.Quantity.Should().Be(2);
    }

    [Fact]
    public void AnEffectProductWithNoUsableIdHasNoSprite()
    {
        new CatalogProductEntity
        {
            ProductType = ProductType.Effect,
            ExtraParam = "not-a-number",
            Quantity = 1,
            CatalogOfferEntityId = OFFER_ID,
            Offer = null!,
        }
            .ToSnapshot(null, null)
            .SpriteId.Should()
            .Be(-1);
    }

    [Theory]
    [InlineData("7", true, 7)]
    [InlineData("108", true, 108)]
    [InlineData("0", false, 0)]
    [InlineData("-1", false, 0)]
    [InlineData("+5", false, 0)]
    [InlineData("1e3", false, 0)]
    [InlineData(" 7", false, 0)]
    [InlineData("", false, 0)]
    [InlineData(null, false, 0)]
    public void OnlyAWholeNumberAboveZeroNamesAnEffect(string? extraParam, bool names, int id)
    {
        EffectProducts.TryGetEffectId(extraParam, out var effectId).Should().Be(names);
        effectId.Should().Be(names ? id : effectId);
    }

    [Fact]
    public void CopiesAreCountedPerEffectAndMultipliedByHowManyTimesTheOfferIsBought()
    {
        var copies = EffectProducts.CountCopies(
            [
                Product(ProductType.Effect, "7", 2, id: 1),
                Product(ProductType.Effect, "9", 1, id: 2),
                // Another product of the same effect adds to it, as the grant will.
                Product(ProductType.Effect, "7", 3, id: 3),
            ],
            quantity: 4
        );

        copies.Should().BeEquivalentTo(new Dictionary<int, long> { [7] = 20, [9] = 4 });
    }

    [Fact]
    public void ProductsThatAreNotEffectsAreNotCounted()
    {
        EffectProducts
            .CountCopies(
                [Product(ProductType.Floor, "7", 5), Product(ProductType.Badge, "ADM", 1, id: 2)],
                quantity: 1
            )
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void AnOfferWithAnEffectProductThatNamesNoEffectCountsAsNothingToGive()
    {
        EffectProducts
            .CountCopies(
                [
                    Product(ProductType.Effect, "7", 1),
                    Product(ProductType.Effect, "seven", 1, id: 2),
                ],
                quantity: 1
            )
            .Should()
            .BeNull();
    }

    [Fact]
    public void CopiesAreCountedInAWideTypeSoALargeQuantityCannotWrapAround()
    {
        var copies = EffectProducts.CountCopies(
            [Product(ProductType.Effect, "7", int.MaxValue)],
            quantity: 4
        );

        copies![7].Should().Be(4L * int.MaxValue);
    }

    [Theory]
    [InlineData(EffectGrantResult.AlreadyPermanent, CatalogPurchaseErrorType.EffectOwned)]
    [InlineData(EffectGrantResult.LimitReached, CatalogPurchaseErrorType.InventoryFull)]
    [InlineData(EffectGrantResult.Invalid, CatalogPurchaseErrorType.PurchaseFailed)]
    public void ARefusedGrantIsWordedWithTheClientsOwnPurchaseError(
        EffectGrantResult refusal,
        CatalogPurchaseErrorType expected
    ) => EffectProducts.ErrorFor(refusal).Should().Be(expected);
}
