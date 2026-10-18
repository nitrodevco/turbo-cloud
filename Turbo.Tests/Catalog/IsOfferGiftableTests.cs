using FluentAssertions;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Catalog;
using Turbo.Primitives.Catalog;
using Turbo.Primitives.Messages.Incoming.Catalog;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Networking;
using Turbo.Tests.Support;
using Xunit;
using static Turbo.Tests.Catalog.CatalogFixture;

namespace Turbo.Tests.Catalog;

/// <summary>
/// GetIsOfferGiftable is answered with IsOfferGiftable (offer id, giftable), the message the client
/// reads for it, carrying the offer's can_gift as its catalogue page shows it. It went unanswered.
/// </summary>
public sealed class IsOfferGiftableTests : IDisposable
{
    private readonly CatalogFixture _catalog = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _catalog.Dispose();

    private async Task<IReadOnlyList<object?>> AskAsync(int offerId, bool canGift = true)
    {
        var provider = _catalog.NormalProvider();

        await provider.ReloadAsync(Ct);

        var snapshot = provider.Current with
        {
            OffersById = provider.Current.OffersById.SetItem(
                SOLD,
                provider.Current.OffersById[SOLD] with
                {
                    CanGift = canGift,
                }
            ),
        };
        var fakes = new Fakes();

        fakes.Handlers["GetCatalogSnapshot"] = _ => snapshot;

        await new GetIsOfferGiftableMessageHandler(fakes.Create<ICatalogService>()).HandleAsync(
            new GetIsOfferGiftableMessage { OfferId = offerId },
            new MessageContext(fakes.Create<ISessionContext>(), 1, 7),
            Ct
        );

        return [.. fakes.Log.Of("SendComposerAsync").Select(x => x.Args[0])];
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AnOffer_IsAnsweredWithItsCanGift(bool canGift)
    {
        (await AskAsync(SOLD, canGift))
            .Should()
            .ContainSingle()
            .Which.Should()
            .BeEquivalentTo(
                new IsOfferGiftableMessageComposer { OfferId = SOLD, IsGiftable = canGift }
            );
    }

    [Fact]
    public async Task AnOfferTheCatalogueDoesNotHold_IsAnsweredNothing()
    {
        (await AskAsync(9999)).Should().BeEmpty();
    }

    [Fact]
    public void TheAnswer_IsTheOfferIdThenTheFlag()
    {
        var packet = PacketHarness.Encode(
            new IsOfferGiftableMessageComposer { OfferId = SOLD, IsGiftable = true }
        );

        packet.PopInt().Should().Be(SOLD);
        packet.PopBoolean().Should().BeTrue();
        packet.Remaining.Should().Be(0);
    }
}
