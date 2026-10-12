using Microsoft.Extensions.Options;
using Turbo.Catalog.Configuration;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Marketplace;
using Turbo.Primitives.Messages.Incoming.Marketplace;
using Turbo.Primitives.Messages.Outgoing.Marketplace;
using Turbo.Primitives.Networking;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// Habbo answers GetMarketplaceConfiguration (hhus, 2026-10-12: on, commission 1, prices 2 to
/// 99999, 168 hours, average over 7 days, fee 2%, limits 100000 / 400000); Turbo sent nothing.
/// </summary>
public sealed class MarketplaceConfigurationTests
{
    [Fact]
    public async Task the_marketplace_settings_are_sent_and_written_in_habbos_order()
    {
        var fakes = new Fakes();

        await new GetMarketplaceConfigurationMessageHandler(
            Options.Create(new CatalogConfig { Marketplace = new() { Enabled = true } })
        ).HandleAsync(
            new GetMarketplaceConfigurationMessage(),
            new MessageContext(fakes.Create<ISessionContext>(), 1, -1),
            TestContext.Current.CancellationToken
        );

        var composer = Assert.IsType<MarketplaceConfigurationEventMessageComposer>(
            fakes.Log.Of("SendComposerAsync").Single().Args[0]
        );
        var packet = PacketHarness.Encode(composer);

        // The official reply, byte for byte.
        Assert.Equal(
            Convert.FromHexString(
                "0100000001000000000000000000000002"
                    + "0001869F000000A80000000700000002000186A000061A80"
            ),
            packet.PopBytes(packet.Remaining)
        );
    }
}
