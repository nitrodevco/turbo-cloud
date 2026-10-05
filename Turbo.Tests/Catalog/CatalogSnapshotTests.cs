using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Turbo.Catalog;
using Turbo.Catalog.Exceptions;
using Turbo.Database.Entities.Catalog;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Catalog.Providers;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Catalog.Tags;
using Turbo.Primitives.Players.Enums.Wallet;
using Turbo.Primitives.Players.Wallet;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// The catalog as players get it from the database: an offer priced in a currency is charged
/// that currency's activity-point type, not its row number; and an offer the editor hid, or one
/// on a hidden page, is neither listed nor sold.
/// </summary>
public sealed class CatalogSnapshotTests : IDisposable
{
    private readonly CatalogFixture _catalog = new();
    private readonly CapturingLogger<ICatalogSnapshotProvider<NormalCatalog>> _log = new();
    private readonly List<WalletDebitRequest> _charged = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _catalog.Dispose();

    private async Task<CatalogSnapshot> LoadAsync()
    {
        var provider = _catalog.NormalProvider(_log);

        await provider.ReloadAsync(Ct);

        return provider.Current;
    }

    [Fact]
    public async Task AnOfferInACurrency_IsPricedInThatCurrencysActivityPointType()
    {
        var snapshot = await LoadAsync();

        // Duckets are row 4 and type 0: charging type 4 charged a currency nobody has.
        snapshot.OffersById[IN_DUCKETS].ActivityPointType.Should().Be(0);
        snapshot.OffersById[IN_DIAMONDS].ActivityPointType.Should().Be(5);
        snapshot.OffersById[SOLD].ActivityPointType.Should().BeNull();
    }

    [Fact]
    public async Task AnOfferInACurrencyThatIsNotActivityPoints_ChargesNone_AndSaysSo()
    {
        var snapshot = await LoadAsync();

        snapshot.OffersById[IN_CREDITS_ROW].ActivityPointType.Should().BeNull();
        _log.AtLeast(LogLevel.Warning)
            .Should()
            .ContainSingle()
            .Which.Message.Should()
            .Contain($"{IN_CREDITS_ROW}");
    }

    [Fact]
    public async Task AHiddenOffer_IsOnNoPage_ButStillKnownById()
    {
        var snapshot = await LoadAsync();

        snapshot.PagesById[FURNITURE].OfferIds.Should().NotContain(HIDDEN_OFFER).And.Contain(SOLD);
        snapshot.OffersById.Should().ContainKey(HIDDEN_OFFER);
    }

    [Fact]
    public async Task BuyingInDuckets_DebitsDuckets()
    {
        var bought = await BuyAsync(IN_DUCKETS);

        bought.Id.Should().Be(IN_DUCKETS);
        _charged
            .Should()
            .ContainSingle()
            .Which.Should()
            .Be(
                new WalletDebitRequest { CurrencyKind = CurrencyKind.ActivityPoints(0), Amount = 3 }
            );
    }

    [Fact]
    public async Task AHiddenOffer_IsNotForSale()
    {
        var buy = () => BuyAsync(HIDDEN_OFFER);

        (await buy.Should().ThrowAsync<CatalogPurchaseException>())
            .Which.ErrorType.Should()
            .Be(CatalogPurchaseErrorType.OfferNotFound);
        _charged.Should().BeEmpty("nothing is charged for an offer that is not sold");
    }

    [Fact]
    public async Task AnOfferOnAHiddenPage_IsStillSold_AsTheClubWindowSellsMemberships()
    {
        (await BuyAsync(ON_HIDDEN_PAGE)).Id.Should().Be(ON_HIDDEN_PAGE);
        _charged.Should().ContainSingle().Which.Amount.Should().Be(5);
    }

    [Fact]
    public async Task AHiddenLimitedOffer_CanNotBeEntered()
    {
        _catalog.Db.Insert(
            new LtdSeriesEntity
            {
                Id = 9,
                CatalogProductEntityId = HIDDEN_OFFER,
                TotalQuantity = 10,
                RemainingQuantity = 10,
                RaffleWindowSeconds = 0,
                IsActive = true,
            }
        );

        var snapshot = await LoadAsync();
        var fakes = _catalog.Fakes;

        fakes.Handlers["GetCatalogSnapshot"] = _ => snapshot;

        var grain = GrainHarness.Create(
            typeof(CatalogModule).Assembly,
            "Turbo.Catalog.Grains.CatalogLtdRaffleGrain",
            fakes,
            _catalog.Db
        );

        RoomHarness.SetMember(
            grain
                .GetType()
                .GetField("_state", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(grain)!,
            "SeriesId",
            9
        );
        Set(grain, "_catalogService", fakes.Create<ICatalogService>());
        await ((Grain)grain).OnActivateAsync(Ct);

        var entry = await ((ICatalogLtdRaffleGrain)grain).EnterRaffleAsync(7, Ct);

        entry.Success.Should().BeFalse();
        entry.Error.Should().Be(LtdRaffleEntryErrorType.SeriesNotFound);
        _charged.Should().BeEmpty();
    }

    /// <summary>The player's purchase grain buying an offer from the catalog as loaded.</summary>
    private async Task<CatalogOfferSnapshot> BuyAsync(int offerId)
    {
        var snapshot = await LoadAsync();
        var fakes = _catalog.Fakes;

        fakes.Handlers["GetCatalogSnapshot"] = _ => snapshot;
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

        Set(grain, "_catalogService", fakes.Create<ICatalogService>());
        Set(grain, "_definitionProvider", _catalog.Definitions);
        // Keyed by the buyer, as Orleans keys it: the grain reads who is buying from its key.
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

        return await ((ICatalogPurchaseGrain)grain).PurchaseOfferFromCatalogAsync(
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
