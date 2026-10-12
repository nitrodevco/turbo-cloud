using Microsoft.Extensions.Options;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Hotlooks;
using Turbo.Players.Configuration;
using Turbo.Primitives.Messages.Incoming.Hotlooks;
using Turbo.Primitives.Messages.Outgoing.Hotlooks;
using Turbo.Primitives.Networking;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// The avatar editor's Hot Looks tab (hhus, 2026-10-12): the client sends GetHotLooks with one
/// byte, 20, and Habbo answers with 20 looks (gender m / f, figure). Turbo parsed no amount and
/// answered nothing.
/// </summary>
public sealed class HotLooksTests
{
    [Fact]
    public async Task the_hotels_hot_looks_are_sent_up_to_the_amount_asked_for()
    {
        var request = Assert.IsType<GetHotLooksMessage>(
            PacketHarness.Parse(PacketHarness.Incoming("GetHotLooksMessageEvent"), [0x02])
        );
        var fakes = new Fakes();
        var config = new PlayerConfig
        {
            HotLooks =
            [
                new() { Gender = "m", Figure = "hr-165-34.hd-209-1" },
                new() { Gender = "f", Figure = "hr-515-42.hd-600-1" },
                new() { Gender = "m", Figure = "hr-100-1.hd-180-1" },
            ],
        };

        await new GetHotLooksMessageHandler(Options.Create(config)).HandleAsync(
            request,
            new MessageContext(fakes.Create<ISessionContext>(), 1, -1),
            TestContext.Current.CancellationToken
        );

        var composer = Assert.IsType<HotLooksMessageComposer>(
            fakes.Log.Of("SendComposerAsync").Single().Args[0]
        );
        var packet = PacketHarness.Encode(composer);

        Assert.Equal(2, request.Amount);
        Assert.Equal(2, packet.PopInt());
        Assert.Equal(("m", "hr-165-34.hd-209-1"), (packet.PopString(), packet.PopString()));
        Assert.Equal(("f", "hr-515-42.hd-600-1"), (packet.PopString(), packet.PopString()));
        Assert.Equal(0, packet.Remaining);
    }
}
