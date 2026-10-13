using FluentAssertions;
using Orleans;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Catalog;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// GetHabboClubExtendOffer asks for the renewal the toolbar's extend promo and the club centre
/// offer. Habbo answered a non-member nothing (hhus, 2026-10-13); Turbo sent them the offer.
/// </summary>
public sealed class ClubExtendOfferTests
{
    private static async Task<IReadOnlyList<object?>> AskAsync(int secondsLeft)
    {
        var fakes = new Fakes();

        fakes.Handlers["GetClubExtendOffer"] = _ => new ClubExtendOfferSnapshot
        {
            OfferId = 1,
            ProductCode = "HABBO_CLUB_VIP_3_MONTHS",
            PriceCredits = 75,
            PriceActivityPoints = 0,
            PriceActivityPointType = 0,
            IsVip = true,
            Months = 3,
            ExtraDays = 0,
            IsGiftable = false,
            DaysLeftAfterPurchase = 0,
            Year = 0,
            Month = 0,
            Day = 0,
            Days = 93,
            OriginalPriceCreditsPerPeriod = 30,
            OriginalPriceActivityPointsPerPeriod = 0,
            OriginalActivityPointType = 0,
            SubscriptionDaysLeft = 0,
        };
        fakes.Handlers["GetAsync"] = _ =>
            Task.FromResult(
                new PlayerSubscriptionSnapshot
                {
                    SubscriptionType = SubscriptionType.HabboClub,
                    SecondsLeft = secondsLeft,
                    SecondsLeftWithGrace = secondsLeft,
                    HasEverBeenMember = secondsLeft > 0,
                    TotalDaysSubscribed = 31,
                    DaysRemaining = (int)Math.Ceiling(secondsLeft / 86400d),
                    FurniLimit = 0,
                    MaxFurniLimit = 0,
                    MinutesSinceLastModified = -1,
                    ExpiresAt = DateTime.UtcNow.AddSeconds(secondsLeft),
                }
            );

        await new GetHabboClubExtendOfferMessageHandler(
            fakes.Create<IGrainFactory>(),
            fakes.Create<ICatalogService>()
        ).HandleAsync(
            new GetHabboClubExtendOfferMessage(),
            new MessageContext(fakes.Create<ISessionContext>(), 1, 7),
            TestContext.Current.CancellationToken
        );

        return [.. fakes.Log.Of("SendComposerAsync").Select(x => x.Args[0])];
    }

    [Fact]
    public async Task a_non_member_is_answered_nothing()
    {
        (await AskAsync(0)).Should().BeEmpty();
    }

    [Fact]
    public async Task a_member_is_offered_the_renewal()
    {
        (await AskAsync(2 * 86400)).Should().ContainSingle().Which.Should().BeOfType<HabboClubExtendOfferMessageComposer>();
    }
}
