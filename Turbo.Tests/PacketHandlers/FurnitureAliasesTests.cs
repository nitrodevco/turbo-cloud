using Microsoft.Extensions.Options;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Room.Engine;
using Turbo.Primitives.Messages.Incoming.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Networking;
using Turbo.Rooms.Configuration;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

/// <summary>
/// Habbo answers GetFurnitureAliases with its 14 advertising aliases (hhus, 2026-10-12); Turbo
/// sends the hotel's own from Turbo:Rooms:FurnitureAliases.
/// </summary>
public sealed class FurnitureAliasesTests
{
    [Fact]
    public async Task the_hotels_aliases_are_sent()
    {
        var fakes = new Fakes();
        var config = new RoomConfig
        {
            FurnitureAliases = new()
            {
                ["ads_711"] = "ads_711c",
                ["ads_pepsi0"] = "ads_pepsi0_camp",
            },
        };

        await new GetFurnitureAliasesMessageHandler(Options.Create(config)).HandleAsync(
            new GetFurnitureAliasesMessage(),
            new MessageContext(fakes.Create<ISessionContext>(), 1, -1),
            TestContext.Current.CancellationToken
        );

        var composer = Assert.IsType<FurnitureAliasesMessageComposer>(
            fakes.Log.Of("SendComposerAsync").Single().Args[0]
        );
        Assert.Equal(
            [("ads_711", "ads_711c"), ("ads_pepsi0", "ads_pepsi0_camp")],
            composer.Aliases
        );
    }
}
