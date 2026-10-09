using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Turbo.Catalog;
using Turbo.Catalog.Configuration;
using Turbo.Catalog.Reception;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// A catalogue purchase that landed counts for a <c>buy_from_catalogue</c> reward track task
/// (the official client's <c>reward_track_tasks_buy_from_catalogue</c> image, JS 88).
/// </summary>
public sealed class CatalogRewardTrackTests : IDisposable
{
    private const int BUYER = 7;

    private readonly CatalogFixture _catalog = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public CatalogRewardTrackTests()
    {
        _catalog.Db.Insert(
            new PlayerEntity
            {
                Id = BUYER,
                Name = "buyer",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );
        _catalog.Fakes.Handlers["GrantFurnitureAsync"] = _ =>
            Task.FromResult<FurnitureItemSnapshot?>(
                (FurnitureItemSnapshot)
                    RuntimeHelpers.GetUninitializedObject(typeof(FurnitureItemSnapshot))
            );
    }

    public void Dispose() => _catalog.Dispose();

    [Fact]
    public async Task A_purchase_that_landed_counts_as_buying_from_the_catalogue()
    {
        await BuyAsync(SOLD);

        _catalog
            .Fakes.Log.Of("RecordActionAsync")
            .Select(x => x.Args[0])
            .Should()
            .Equal(RewardTrackActionTypes.BUY_FROM_CATALOGUE);
    }

    /// <summary>The buyer's purchase grain buying one of an offer, as <c>BonusRareTests</c> does.</summary>
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
            playerId: BUYER
        );

        Set(grain, "_catalogService", fakes.Create<ICatalogService>());
        Set(grain, "_definitionProvider", _catalog.Definitions);
        Set(
            grain,
            "_bonusRare",
            new BonusRareService(
                _catalog.Db,
                Options.Create(new CatalogConfig()),
                _catalog.Definitions,
                fakes.Create<IGrainFactory>(),
                new ManualTimeProvider(new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero)),
                NullLogger<BonusRareService>.Instance
            )
        );
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
