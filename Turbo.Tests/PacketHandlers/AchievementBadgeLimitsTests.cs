using Turbo.Achievements;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Inventory.Badges;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Messages.Incoming.Inventory.Badges;
using Turbo.Primitives.Messages.Outgoing.Inventory.Badges;
using Turbo.Primitives.Networking;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.PacketHandlers;

public sealed class AchievementBadgeLimitsTests
{
    [Fact]
    public async Task LimitsUseTheCodeSuffixThatAs3PrefixesWithAch()
    {
        var fakes = new Fakes();
        fakes.Handlers["get_Current"] = _ => AchievementDefaults.Definitions;
        var handler = new GetBadgePointLimitsMessageHandler(fakes.Create<IAchievementCatalog>());
        await handler.HandleAsync(
            new GetBadgePointLimitsMessage(),
            new MessageContext(fakes.Create<ISessionContext>(), 1, -1),
            TestContext.Current.CancellationToken
        );
        var composer = Assert.IsType<BadgePointLimitsEventMessageComposer>(
            fakes.Log.Of("SendComposerAsync").Single().Args[0]
        );
        var login = composer.LimitsByBadgeCodePrefix.Single(x => x.BadgeCodePrefix == "Login");
        Assert.Equal(1, login.Levels[0].Level);
        Assert.Equal(5, login.Levels[0].Limit);
        Assert.Equal("ACH_Login1", "ACH_" + login.BadgeCodePrefix + login.Levels[0].Level);
        Assert.DoesNotContain(
            composer.LimitsByBadgeCodePrefix,
            x => x.BadgeCodePrefix.StartsWith("ACH_", StringComparison.Ordinal)
        );
    }
}
