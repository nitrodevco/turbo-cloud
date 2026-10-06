using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using Turbo.Primitives.Players.Permissions;
using Xunit;

namespace Turbo.Tests.Players.Permissions;

public class PermissionRegistryTests
{
    private sealed class TestSource(
        string? prefix,
        IEnumerable<string> nodes,
        IEnumerable<string>? metaKeys = null
    ) : IPermissionNodeSource
    {
        public string? Prefix => prefix;

        public IEnumerable<PermissionNodeDefinition> Nodes =>
            nodes.Select(x => new PermissionNodeDefinition(x, "test"));

        public IEnumerable<PermissionMetaDefinition> MetaKeys =>
            (metaKeys ?? []).Select(x => new PermissionMetaDefinition(x, "test"));
    }

    [Fact]
    public void Constructor_RejectsDuplicateNode()
    {
        var act = () =>
            new PermissionRegistry([
                new CorePermissionNodeSource(),
                new TestSource(null, [PermissionNodes.TRADE]),
            ]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*registered twice*");
    }

    [Fact]
    public void Constructor_RejectsNodesUnderTheMembershipRoot()
    {
        var core = () => new PermissionRegistry([new TestSource(null, ["group.vip"])]);
        var plugin = () => new PermissionRegistry([new TestSource("group", ["group.vip"])]);

        core.Should().Throw<InvalidOperationException>().WithMessage("*membership nodes*");
        plugin.Should().Throw<InvalidOperationException>().WithMessage("*membership nodes*");
    }

    [Fact]
    public void Constructor_RejectsGrantedByDefaultWithAClientLevel()
    {
        var act = () =>
            new PermissionRegistry([
                new SingleNodeSource(
                    new(
                        "casino.table.open",
                        "test",
                        ClientLevel: Turbo.Primitives.Players.Enums.SecurityLevelType.Employee,
                        GrantedByDefault: true
                    )
                ),
            ]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*security level*");
    }

    [Fact]
    public void Constructor_RejectsGrantedByDefaultThatIsExplicitOnly()
    {
        var act = () =>
            new PermissionRegistry([
                new SingleNodeSource(
                    new("casino.table.open", "test", GrantedByDefault: true, ExplicitOnly: true)
                ),
            ]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*explicit only*");
    }

    private sealed class SingleNodeSource(PermissionNodeDefinition definition)
        : IPermissionNodeSource
    {
        public string? Prefix => "casino";

        public IEnumerable<PermissionNodeDefinition> Nodes => [definition];

        public IEnumerable<PermissionMetaDefinition> MetaKeys => [];
    }

    [Fact]
    public void IsCheckable_AcceptsMembershipNodes()
    {
        var registry = new PermissionRegistry([new CorePermissionNodeSource()]);

        registry.IsCheckable("group.vip").Should().BeTrue();
        registry.IsCheckable("group.*").Should().BeFalse();
        registry.IsCheckable("casino.table.open").Should().BeFalse();
    }

    [Fact]
    public void Constructor_RejectsPluginNodeOutsideItsPrefix()
    {
        var act = () => new PermissionRegistry([new TestSource("casino", ["dice.roll"])]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*source prefix*");
    }

    [Fact]
    public void Constructor_RejectsPluginMetaKeyOutsideItsPrefix()
    {
        var act = () => new PermissionRegistry([new TestSource("casino", [], ["limit.tables"])]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*source prefix*");
    }

    [Fact]
    public void Constructor_RejectsPluginPrefixReservedByCore()
    {
        var act = () =>
            new PermissionRegistry([
                new CorePermissionNodeSource(),
                new TestSource("room", ["room.dance"]),
            ]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*reserved by core*");
    }

    [Theory]
    [InlineData("Casino")]
    [InlineData("casino.games")]
    [InlineData("")]
    public void Constructor_RejectsMalformedPrefix(string prefix)
    {
        var act = () => new PermissionRegistry([new TestSource(prefix, [])]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*prefix*");
    }

    [Theory]
    [InlineData("room.*")]
    [InlineData("Room.Enter")]
    public void Constructor_RejectsMalformedNode(string node)
    {
        var act = () => new PermissionRegistry([new TestSource(null, [node])]);

        act.Should().Throw<InvalidOperationException>().WithMessage("*malformed*");
    }

    [Fact]
    public void Constructor_AcceptsPrefixedPluginBesideCore()
    {
        var registry = new PermissionRegistry([
            new CorePermissionNodeSource(),
            new TestSource("casino", ["casino.table.open"], ["casino.limit.tables"]),
        ]);

        registry.IsRegistered("casino.table.open").Should().BeTrue();
        registry.IsRegisteredMetaKey("casino.limit.tables").Should().BeTrue();
        registry.IsRegistered(PermissionNodes.TRADE).Should().BeTrue();
    }

    [Fact]
    public void CoreSource_RegistersEveryNodeConstant()
    {
        var registry = new PermissionRegistry([new CorePermissionNodeSource()]);

        ConstantsOf(typeof(PermissionNodes))
            .Should()
            .OnlyContain(x => registry.IsRegistered(x))
            .And.HaveCount(registry.Nodes.Count);
    }

    [Fact]
    public void CoreSource_RegistersEveryMetaKeyConstant()
    {
        var registry = new PermissionRegistry([new CorePermissionNodeSource()]);

        ConstantsOf(typeof(PermissionMetaKeys))
            .Should()
            .OnlyContain(x => registry.IsRegisteredMetaKey(x))
            .And.HaveCount(registry.MetaKeys.Count);
    }

    [Fact]
    public void CoreSource_ProjectsEachPerkOnce()
    {
        var registry = new PermissionRegistry([new CorePermissionNodeSource()]);

        registry
            .Nodes.Values.Where(x => x.Perk is not null)
            .Select(x => x.Perk)
            .Should()
            .OnlyHaveUniqueItems();
    }

    private static List<string> ConstantsOf(Type type) =>
        [
            .. type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(x => x.IsLiteral && x.FieldType == typeof(string))
                .Select(x => (string)x.GetRawConstantValue()!),
            .. type.GetNestedTypes().SelectMany(ConstantsOf),
        ];
}
