using System;
using System.Collections.Immutable;
using System.Linq;
using FluentAssertions;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Xunit;

namespace Turbo.Tests.Players.Permissions;

public class PermissionProjectionTests
{
    private static readonly PermissionRegistry REGISTRY = new([new CorePermissionNodeSource()]);

    [Fact]
    public void NoNodes_IsLevelNone_AndEveryPerkDenied()
    {
        var client = Project();

        client.SecurityLevel.Should().Be(SecurityLevelType.None);
        client.IsAmbassador.Should().BeFalse();
        client.IsModerator.Should().BeFalse();
        client.Perks.Should().NotBeEmpty().And.OnlyContain(x => !x.IsAllowed);
    }

    [Fact]
    public void Level_IsHighestClientLevelHeld()
    {
        Project(PermissionNodes.Wired.MENU, PermissionNodes.Moderation.TOOL)
            .SecurityLevel.Should()
            .Be(SecurityLevelType.Moderator);
    }

    [Fact]
    public void Level_IgnoresNodesWithoutClientLevel()
    {
        Project(PermissionNodes.Room.ENTER_FULL, PermissionNodes.TRADE)
            .SecurityLevel.Should()
            .Be(SecurityLevelType.None);
    }

    [Theory]
    [InlineData("7", SecurityLevelType.Community)]
    [InlineData("2", SecurityLevelType.Employee)] // a floor never lowers the level
    [InlineData("99", SecurityLevelType.Administrator)]
    [InlineData("-3", SecurityLevelType.Employee)]
    [InlineData("staff", SecurityLevelType.Employee)]
    public void MetaFloor_RaisesButNeverLowers(string floor, SecurityLevelType expected)
    {
        var resolved = Resolved([PermissionNodes.Wired.MENU], floor);

        PermissionProjection.Project(REGISTRY, resolved).SecurityLevel.Should().Be(expected);
    }

    [Fact]
    public void Nodes_AreTheHeldClientFacingOnes_Ordered()
    {
        var client = Project(
            PermissionNodes.Wired.MENU,
            PermissionNodes.Catalog.GIFT_HIDE_SENDER,
            PermissionNodes.Room.ENTER_FULL,
            PermissionNodes.TRADE
        );

        // enter.full is server-only and trade reaches the client as a perk: neither is a gate.
        client
            .Nodes.Should()
            .Equal(PermissionNodes.Catalog.GIFT_HIDE_SENDER, PermissionNodes.Wired.MENU);
    }

    [Fact]
    public void Nodes_IncludeAPluginNodeMarkedClientVisible()
    {
        var registry = new PermissionRegistry([
            new CorePermissionNodeSource(),
            new VisibleSource(),
        ]);
        var resolved = Resolved(["casino.table.open", "casino.table.rig"]);

        PermissionProjection.Project(registry, resolved).Nodes.Should().Equal("casino.table.open");
    }

    private sealed class VisibleSource : IPermissionNodeSource
    {
        public string? Prefix => "casino";

        public System.Collections.Generic.IEnumerable<PermissionNodeDefinition> Nodes =>
            [
                new("casino.table.open", "test", ClientVisible: true),
                new("casino.table.rig", "test"),
            ];

        public System.Collections.Generic.IEnumerable<PermissionMetaDefinition> MetaKeys => [];
    }

    [Fact]
    public void Ambassador_AndModerator_FollowTheirNodes()
    {
        var client = Project(PermissionNodes.Role.AMBASSADOR, PermissionNodes.Room.MODERATE_ANY);

        client.IsAmbassador.Should().BeTrue();
        client.IsModerator.Should().BeTrue();
    }

    [Fact]
    public void Perks_FollowNodes_WithRefusals()
    {
        var client = Project(PermissionNodes.Perk.CAMERA, PermissionNodes.Room.FLOORPLAN_LARGE);

        client.Perks.Single(x => x.Perk == PlayerPerkFlags.Camera).IsAllowed.Should().BeTrue();
        client
            .Perks.Single(x => x.Perk == PlayerPerkFlags.BuilderAtWork)
            .IsAllowed.Should()
            .BeTrue();

        var guide = client.Perks.Single(x => x.Perk == PlayerPerkFlags.UseGuideTool);
        guide.IsAllowed.Should().BeFalse();
        guide.Refusal.Should().Be("requirement.unfulfilled.helper_level_4");
    }

    [Fact]
    public void EveryPerkTheClientReads_IsProjected()
    {
        // The nine codes the Flash client asks isPerkAllowed about (permissions-client-perks.md).
        PlayerPerkFlags[] read =
        [
            PlayerPerkFlags.Camera,
            PlayerPerkFlags.UseGuideTool,
            PlayerPerkFlags.JudgeChatReviews,
            PlayerPerkFlags.Citizen,
            PlayerPerkFlags.MouseZoom,
            PlayerPerkFlags.BuilderAtWork,
            PlayerPerkFlags.NavigatorRoomThumbnailCamera,
            PlayerPerkFlags.NavigatorPhaseTwo2014,
            PlayerPerkFlags.NavigatorPhaseOne2014,
        ];

        Project().Perks.Select(x => x.Perk).Should().Contain(read);
    }

    [Fact]
    public void Matches_ComparesPerksByValue()
    {
        Project(PermissionNodes.Perk.CAMERA)
            .Matches(Project(PermissionNodes.Perk.CAMERA))
            .Should()
            .BeTrue();
        Project(PermissionNodes.Perk.CAMERA).Matches(Project()).Should().BeFalse();
    }

    [Fact]
    public void ReportLevel_NamesTheNodeThatSetIt_AndWhatLeaks()
    {
        var report = PermissionProjection.ReportLevel(
            REGISTRY,
            Resolved([PermissionNodes.Navigator.CATEGORY_STAFF, PermissionNodes.Wired.MENU])
        );

        report.Level.Should().Be(SecurityLevelType.Community);
        report.Source.Should().Be(PermissionNodes.Navigator.CATEGORY_STAFF);

        var refused = report.ShownButRefused.Select(x => x.Node).ToList();
        refused.Should().Contain(PermissionNodes.Navigator.STAFF_PICK);
        refused.Should().Contain(PermissionNodes.Moderation.TOOL);
        refused.Should().NotContain(PermissionNodes.Wired.MENU);
        refused.Should().NotContain(PermissionNodes.Room.ENTER_FULL); // no client level
        report.ShownButRefused.Select(x => x.ClientLevel).Should().BeInDescendingOrder();
    }

    [Fact]
    public void ReportLevel_FromMetaFloor_NamesTheKey()
    {
        var report = PermissionProjection.ReportLevel(REGISTRY, Resolved([], "8"));

        report.Level.Should().Be(SecurityLevelType.Administrator);
        report.Source.Should().Be(PermissionMetaKeys.Client.SECURITY_LEVEL);
    }

    [Fact]
    public void ReportLevel_AtNone_LeaksNothing()
    {
        var report = PermissionProjection.ReportLevel(REGISTRY, Resolved([PermissionNodes.TRADE]));

        report.Source.Should().BeNull();
        report.ShownButRefused.Should().BeEmpty();
    }

    private static PermissionClientSnapshot Project(params string[] nodes) =>
        PermissionProjection.Project(REGISTRY, Resolved(nodes));

    private static ResolvedPermissionsSnapshot Resolved(string[] nodes, string? floor = null) =>
        new()
        {
            Granted = [.. nodes],
            Meta = floor is null
                ? ImmutableDictionary<string, string>.Empty
                : ImmutableDictionary<string, string>.Empty.Add(
                    PermissionMetaKeys.Client.SECURITY_LEVEL,
                    floor
                ),
            UnregisteredNodes = [],
            UnregisteredMetaKeys = [],
        };
}
