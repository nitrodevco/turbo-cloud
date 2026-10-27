using System.Collections.Immutable;
using System.Reflection;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using Turbo.Catalog;
using Turbo.Catalog.Exceptions;
using Turbo.Database.Entities.Players;
using Turbo.Players.Policies;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Events;
using Turbo.Primitives.Catalog.Grains;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Inventory.Snapshots;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Wallet;
using Turbo.Primitives.Policies;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// Several plugins each with a say over a purchase (<see cref="IPurchasePolicy"/>): every one is
/// asked in order until one refuses, a refusal shows the error it chose, one that throws refuses
/// (a plugin there to stop a purchase must not let it through when it breaks), and a club gift
/// claim is asked and told of like any purchase.
/// </summary>
public sealed class PurchasePolicyTests : IDisposable
{
    private const int PLAYER = 7;

    private readonly CatalogFixture _catalog = new();
    private readonly CapturingLogger<GamePolicies> _log = new();
    private readonly GamePolicies _policies;
    private readonly TestEventBus _events = new();
    private readonly List<(string Policy, PurchaseKind Kind)> _asked = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PurchasePolicyTests()
    {
        _policies = new GamePolicies(_log);
        _events.Record<PurchaseCompletedEvent>();
        _catalog.Fakes.Handlers["TryDebitAsync"] = _ =>
            Task.FromResult(WalletDebitResult.Success());
    }

    public void Dispose() => _catalog.Dispose();

    [Fact]
    public async Task Two_policies_that_allow_both_decide_and_the_purchase_lands_with_its_items()
    {
        _policies.Register("alpha", Allowing("alpha"));
        _policies.Register("beta", Allowing("beta"));
        _catalog.Fakes.Handlers["GrantCatalogOfferAsync"] = _ =>
            Task.FromResult(ImmutableArray.Create(Item(501), Item(502)));

        await (await PurchaseGrainAsync()).PurchaseOfferFromCatalogAsync(
            CatalogType.Normal,
            SOLD,
            string.Empty,
            1,
            Ct
        );

        _asked.Should().Equal(("alpha", PurchaseKind.Catalog), ("beta", PurchaseKind.Catalog));
        _catalog.Fakes.Log.Of("TryDebitAsync").Should().ContainSingle();

        await _events.WhenPublishedAsync<PurchaseCompletedEvent>(1);
        var completed = _events.Of<PurchaseCompletedEvent>().Single();

        completed.Kind.Should().Be(PurchaseKind.Catalog);
        completed.OfferId.Should().Be(SOLD);
        completed.CostCredits.Should().Be(5);
        completed.ItemIds.Select(x => x.Value).Should().Equal(501, 502);
    }

    [Fact]
    public async Task A_refusal_is_the_answer_shows_its_error_and_the_policies_after_it_are_not_asked()
    {
        _policies.Register("alpha", Allowing("alpha"));
        _policies.Register(
            "beta",
            new Policy(
                "beta",
                _asked,
                _ => PurchaseDecision.Refuse(error: CatalogPurchaseErrorType.RequiresHabboClub)
            )
        );
        _policies.Register("gamma", Allowing("gamma"));

        var buy = async () =>
            await (await PurchaseGrainAsync()).PurchaseOfferFromCatalogAsync(
                CatalogType.Normal,
                SOLD,
                string.Empty,
                1,
                Ct
            );

        var refused = (await buy.Should().ThrowAsync<CatalogPurchaseException>()).Which;

        refused.ErrorType.Should().Be(CatalogPurchaseErrorType.RequiresHabboClub);
        refused.Refusal.Should().BeNull();
        _asked.Select(x => x.Policy).Should().Equal("alpha", "beta");
        NothingMoved();
    }

    // Was passed over: a throwing policy let the purchase go on at the price so far.
    [Fact]
    public async Task A_policy_that_throws_refuses_the_purchase_before_any_money_moves()
    {
        _policies.Register(
            "alpha",
            new Policy("alpha", _asked, _ => throw new InvalidOperationException("db down"))
        );
        _policies.Register("beta", Allowing("beta"));

        var buy = async () =>
            await (await PurchaseGrainAsync()).PurchaseOfferFromCatalogAsync(
                CatalogType.Normal,
                SOLD,
                string.Empty,
                1,
                Ct
            );

        (await buy.Should().ThrowAsync<CatalogPurchaseException>())
            .Which.ErrorType.Should()
            .Be(CatalogPurchaseErrorType.PurchaseFailed);
        _asked.Select(x => x.Policy).Should().Equal("alpha");
        NothingMoved();
        _log.AtLeast(LogLevel.Error)
            .Should()
            .ContainSingle()
            .Which.Message.Should()
            .Contain("refused");
    }

    [Fact]
    public async Task A_club_gift_claim_a_policy_refuses_spends_no_gift_and_grants_nothing()
    {
        _policies.Register(
            "alpha",
            new Policy("alpha", _asked, _ => PurchaseDecision.Refuse("Not today."))
        );

        var claim = async () => await (await ClubGiftGrainAsync()).ClaimClubGiftAsync("gift", Ct);

        (await claim.Should().ThrowAsync<CatalogPurchaseException>())
            .Which.Refusal.Should()
            .Be("Not today.");
        _asked.Should().Equal(("alpha", PurchaseKind.ClubGiftClaim));
        _catalog.Fakes.Log.Of("GrantCatalogOfferAsync").Should().BeEmpty();

        await using var db = await _catalog.Db.CreateDbContextAsync(Ct);

        (await db.PlayerClubGifts.CountAsync(Ct)).Should().Be(0, "the gift is still theirs");
    }

    [Fact]
    public async Task A_club_gift_claimed_is_a_completed_purchase_of_its_kind_that_cost_nothing()
    {
        _catalog.Fakes.Handlers["GrantCatalogOfferAsync"] = _ =>
            Task.FromResult(ImmutableArray.Create(Item(601)));

        await (await ClubGiftGrainAsync()).ClaimClubGiftAsync("gift", Ct);

        await _events.WhenPublishedAsync<PurchaseCompletedEvent>(1);
        var completed = _events.Of<PurchaseCompletedEvent>().Single();

        completed.Kind.Should().Be(PurchaseKind.ClubGiftClaim);
        completed.CostCredits.Should().Be(0);
        completed.ItemIds.Select(x => x.Value).Should().Equal(601);
    }

    private Policy Allowing(string name) => new(name, _asked, proposed => proposed);

    private void NothingMoved()
    {
        _catalog.Fakes.Log.Of("TryDebitAsync").Should().BeEmpty("nothing is charged");
        _catalog.Fakes.Log.Of("GrantCatalogOfferAsync").Should().BeEmpty("nothing is given");
        _events.Of<PurchaseCompletedEvent>().Should().BeEmpty();
    }

    /// <summary>
    /// A member with gifts to claim, the offer <see cref="SOLD"/> on the club gift shelf as
    /// product code "gift".
    /// </summary>
    private async Task<ICatalogPurchaseGrain> ClubGiftGrainAsync()
    {
        _catalog.Db.Insert(
            new PlayerEntity
            {
                Id = PLAYER,
                Name = "member",
                Figure = "hd-180-1",
                Gender = AvatarGenderType.Male,
                PlayerStatus = PlayerStatusType.Offline,
            }
        );

        var grain = await PurchaseGrainAsync();

        // The harness skips the constructor, which reads it from the player settings.
        Set(grain, "_giftIntervalDays", 31);

        var shelf = _catalog.NormalProvider();

        await shelf.ReloadAsync(Ct);

        var gift = shelf.Current.OffersById[SOLD] with
        {
            LocalizationId = "gift",
            ClubGiftDaysRequired = 0,
        };

        _catalog.Fakes.Handlers["GetClubGiftOffers"] = _ => ImmutableArray.Create(gift);
        _catalog.Fakes.Handlers["GetAsync"] = _ =>
            Task.FromResult(
                new PlayerSubscriptionSnapshot
                {
                    SubscriptionType = SubscriptionType.HabboClub,
                    SecondsLeft = 86400,
                    SecondsLeftWithGrace = 86400,
                    HasEverBeenMember = true,
                    TotalDaysSubscribed = 365,
                    DaysRemaining = 1,
                    FurniLimit = 0,
                    MaxFurniLimit = 0,
                    MinutesSinceLastModified = 0,
                    ExpiresAt = null,
                }
            );

        return grain;
    }

    /// <summary>The player's purchase grain over the catalog as published, with these policies.</summary>
    private async Task<ICatalogPurchaseGrain> PurchaseGrainAsync()
    {
        var provider = _catalog.NormalProvider();

        await provider.ReloadAsync(Ct);

        var fakes = _catalog.Fakes;

        fakes.Handlers["GetCatalogSnapshot"] = _ => provider.Current;

        var grain = GrainHarness.Create(
            typeof(CatalogModule).Assembly,
            "Turbo.Catalog.Grains.CatalogPurchaseGrain",
            fakes,
            _catalog.Db,
            playerId: PLAYER
        );

        Set(grain, "_catalogService", fakes.Create<ICatalogService>());
        Set(grain, "_definitionProvider", _catalog.Definitions);
        GrainHarness.Replace<IGamePolicies>(grain, _policies);
        GrainHarness.Replace(grain, _events.System);
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
                            GrainIdKeyExtensions.CreateIntegerKey(PLAYER)
                        )
                    ),
                ]
            );

        return (ICatalogPurchaseGrain)grain;
    }

    private FurnitureItemSnapshot Item(int id)
    {
        var definition = _catalog.Definitions.TryGetDefinition(CHAIR)!;

        return new FurnitureItemSnapshot
        {
            ItemId = id,
            SpriteId = definition.SpriteId,
            OwnerId = PLAYER,
            OwnerName = "buyer",
            Definition = definition,
            StuffData = new LegacyStuffSnapshot { StuffBitmask = 0, Data = "0" },
            ExtraData = string.Empty,
            SecondsToExpiration = -1,
            HasRentPeriodStarted = false,
            RoomId = -1,
        };
    }

    private static void Set(object grain, string field, object value) =>
        grain
            .GetType()
            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(grain, value);

    /// <summary>Records each time it is asked, then decides with <paramref name="decide"/>.</summary>
    private sealed class Policy(
        string name,
        List<(string Policy, PurchaseKind Kind)> asked,
        Func<PurchaseDecision, PurchaseDecision> decide
    ) : IPurchasePolicy
    {
        public ValueTask<PurchaseDecision> DecideAsync(
            PurchaseRequest request,
            PurchaseDecision proposed,
            CancellationToken ct
        )
        {
            asked.Add((name, request.Kind));

            return ValueTask.FromResult(decide(proposed));
        }
    }
}
