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
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// The bonus rare: credits a player brings in - spent in the catalogue, or bought and recorded
/// under a receipt, as the running campaign counts - add up, every target reached gives the
/// furniture once, and the widget reads how far there is to go as its
/// <c>BonusRareInfoMessageParser</c> does.
/// </summary>
public sealed class BonusRareTests : IDisposable
{
    private const int BUYER = 7;
    private const int REWARD = 30;

    private static readonly DateTime NOW = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);

    private readonly CatalogFixture _catalog = new();
    private readonly ManualTimeProvider _time = new(new DateTimeOffset(NOW));
    private readonly List<int> _granted = [];
    private readonly BonusRareService _bonusRare;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public BonusRareTests()
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
        _catalog.AddDefinition(REWARD, "bonusbag_test");
        _catalog.Fakes.Handlers["GrantFurnitureAsync"] = call =>
        {
            _granted.Add((int)call.Args[0]!);

            return Task.FromResult<FurnitureItemSnapshot?>(
                (FurnitureItemSnapshot)
                    RuntimeHelpers.GetUninitializedObject(typeof(FurnitureItemSnapshot))
            );
        };

        _bonusRare = new BonusRareService(
            _catalog.Db,
            Options.Create(new CatalogConfig()),
            _catalog.Definitions,
            _catalog.Fakes.Create<IGrainFactory>(),
            _time,
            NullLogger<BonusRareService>.Instance
        );
    }

    public void Dispose() => _catalog.Dispose();

    [Fact]
    public async Task credits_spent_in_the_catalogue_add_up_and_each_target_gives_the_reward_once()
    {
        await _bonusRare.SaveAsync(Campaign(BonusRareSource.CatalogSpending, required: 8), Ct);

        // SOLD costs five credits.
        await BuyAsync(SOLD);

        _granted.Should().BeEmpty();
        (await InfoAsync()).Should().Be(("bonus_product", REWARD, 8, 3));

        await BuyAsync(SOLD);

        _granted.Should().Equal(REWARD);
        (await InfoAsync()).Should().Be(("bonus_product", REWARD, 8, 6), "two credits are over");
        _catalog
            .Fakes.Log.Of("SendComposerAsync")
            .Select(x => x.Args[0])
            .OfType<BonusRareInfoMessageComposer>()
            .Last()
            .CoinsStillRequiredToBuy.Should()
            .Be(6, "the widget is told where the buyer stands");
        (await _bonusRare.GetStandingAsync("bonus26", Ct)).RewardsGiven.Should().Be(1);
    }

    [Fact]
    public async Task bought_credits_count_once_per_receipt_and_catalogue_spending_does_not()
    {
        await _bonusRare.SaveAsync(Campaign(BonusRareSource.PurchasedCredits, required: 8), Ct);

        await BuyAsync(SOLD);
        (await InfoAsync()).CoinsLeft.Should().Be(8, "spending counts only when the campaign says");

        (await _bonusRare.RecordPurchaseAsync(new PlayerId(BUYER), 20, "order-1", Ct))
            .Should()
            .Be(BonusRarePurchaseResult.Recorded);
        (await _bonusRare.RecordPurchaseAsync(new PlayerId(BUYER), 20, "order-1", Ct))
            .Should()
            .Be(BonusRarePurchaseResult.AlreadyRecorded);

        _granted.Should().Equal(REWARD, REWARD);
        (await InfoAsync()).CoinsLeft.Should().Be(4);
    }

    [Fact]
    public async Task a_reward_that_cant_be_given_keeps_the_credits_for_later()
    {
        await _bonusRare.SaveAsync(Campaign(BonusRareSource.PurchasedCredits, required: 8), Ct);
        _catalog.Fakes.Handlers["GrantFurnitureAsync"] = _ =>
            Task.FromResult<FurnitureItemSnapshot?>(null);

        await _bonusRare.RecordPurchaseAsync(new PlayerId(BUYER), 10, "order-2", Ct);

        (await _bonusRare.GetStandingAsync("bonus26", Ct)).RewardsGiven.Should().Be(0);
        (await InfoAsync()).CoinsLeft.Should().Be(0, "the target is still reached");
    }

    [Fact]
    public async Task with_no_campaign_running_the_widget_is_hidden()
    {
        await _bonusRare.SaveAsync(
            Campaign(BonusRareSource.CatalogSpending, required: 8) with
            {
                StartsAt = NOW.AddDays(-3),
                EndsAt = NOW.AddDays(-1),
            },
            Ct
        );

        (await InfoAsync()).ClassId.Should().Be(-1);
        (await _bonusRare.RecordPurchaseAsync(new PlayerId(BUYER), 20, "order-3", Ct))
            .Should()
            .Be(BonusRarePurchaseResult.NoCampaign);
    }

    [Theory]
    [InlineData("", "bonusbag_test", 8)]
    [InlineData("code", "no_such_furni", 8)]
    [InlineData("code", "bonusbag_test", 0)]
    public async Task a_campaign_it_cant_run_is_refused(string code, string furniture, int required)
    {
        var save = () =>
            _bonusRare.SaveAsync(
                Campaign(BonusRareSource.CatalogSpending, required) with
                {
                    Code = code,
                    FurnitureName = furniture,
                },
                Ct
            );

        await save.Should().ThrowAsync<ArgumentException>();
    }

    private static BonusRareCampaignSnapshot Campaign(BonusRareSource source, int required) =>
        new()
        {
            Id = 0,
            Code = "bonus26",
            FurnitureName = "bonusbag_test",
            ProductCode = "bonus_product",
            CreditsRequired = required,
            Source = source,
            StartsAt = NOW.AddDays(-1),
            EndsAt = null,
        };

    /// <summary>The widget's four fields, as the client reads them.</summary>
    private async Task<(string Product, int ClassId, int Total, int CoinsLeft)> InfoAsync()
    {
        var harness = new PacketHarness();

        harness.Resolver.Overrides[typeof(IBonusRareService)] = _bonusRare;

        var replies = await harness.SendAsync(
            PacketHarness.Incoming("GetBonusRareInfoMessageEvent"),
            PacketHarness.Payload(_ => { }),
            playerId: BUYER
        );
        var packet = replies.Single(x =>
            x.Header == PacketHarness.Outgoing("BonusRareInfoMessageComposer")
        );
        var info = (packet.PopString(), packet.PopInt(), packet.PopInt(), packet.PopInt());

        packet.Remaining.Should().Be(0);

        return info;
    }

    /// <summary>The buyer's purchase grain buying one of an offer, as <c>ProductParamPurchaseTests</c> does.</summary>
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
        Set(grain, "_bonusRare", _bonusRare);
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
