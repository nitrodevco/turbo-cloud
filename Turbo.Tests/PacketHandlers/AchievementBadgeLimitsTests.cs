using System.Collections.Immutable;
using Turbo.Achievements;
using Turbo.Messages.Registry;
using Turbo.PacketHandlers.Inventory.Badges;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Enums;
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

    [Fact]
    public async Task LimitsCoverEveryStateTheClientKnowsAndSkipDisabled()
    {
        var fakes = new Fakes();
        fakes.Handlers["get_Current"] = _ =>
            ImmutableArray.Create(
                Definition(1, "Alpha", AchievementState.Enabled),
                Definition(2, "Beta", AchievementState.Archived),
                Definition(3, "Gamma", AchievementState.OffSeason),
                Definition(4, "Delta", AchievementState.Disabled)
            );
        var handler = new GetBadgePointLimitsMessageHandler(fakes.Create<IAchievementCatalog>());

        await handler.HandleAsync(
            new GetBadgePointLimitsMessage(),
            new MessageContext(fakes.Create<ISessionContext>(), 1, -1),
            TestContext.Current.CancellationToken
        );

        var composer = Assert.IsType<BadgePointLimitsEventMessageComposer>(
            fakes.Log.Of("SendComposerAsync").Single().Args[0]
        );
        Assert.Equal(
            ["Alpha", "Beta", "Gamma"],
            composer.LimitsByBadgeCodePrefix.Select(x => x.BadgeCodePrefix).Order()
        );
    }

    private static AchievementDefinition Definition(int id, string name, AchievementState state) =>
        new()
        {
            Id = id,
            Key = name.ToLowerInvariant(),
            Revision = 1,
            Category = "identity",
            Source = AchievementSources.FIGURE,
            Reducer = AchievementReducer.Counter,
            State = state,
            Levels = [new() { Requirement = 1, BadgeCode = $"ACH_{name}1" }],
        };
}
