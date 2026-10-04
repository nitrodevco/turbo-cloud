using System.Collections.Immutable;
using FluentAssertions;
using Orleans;
using Turbo.Admin.Permissions;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Admin;

/// <summary>
/// What a staff member may change in the panel's permission editor: with
/// <c>permissions.manage</c>, only groups and players lighter than their heaviest group, and only
/// nodes they hold themselves, as <c>docs/permissions.md</c> asks of any editor beyond the console.
/// </summary>
public sealed class PermissionEditPolicyTests
{
    private static readonly PlayerId OWNER = new(1);
    private static readonly PlayerId MANAGER = new(2);
    private static readonly PlayerId MODERATOR = new(3);
    private static readonly PlayerId HELPER = new(4);

    private static readonly PermissionRegistry REGISTRY = new([new CorePermissionNodeSource()]);

    private static readonly string[] ROOM_NODES =
    [
        .. REGISTRY.Nodes.Keys.Where(x => x.StartsWith("room.", StringComparison.Ordinal)),
    ];

    private readonly Fakes _fakes = new();

    private readonly Dictionary<long, string[]> _granted = new()
    {
        // The owner's group holds `*`: every registered node.
        [OWNER.Value] = [.. REGISTRY.Nodes.Keys, .. Memberships("admin", "manager", "moderator")],
        [MANAGER.Value] =
        [
            PermissionNodes.Permissions.MANAGE,
            .. ROOM_NODES,
            .. Memberships("manager", "moderator", "helper"),
        ],
        // Heavier than the helper, but not a manager of permissions.
        [MODERATOR.Value] = [.. ROOM_NODES, .. Memberships("moderator", "helper")],
        [HELPER.Value] = [.. Memberships("helper")],
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PermissionEditPolicyTests()
    {
        _fakes.Handlers["get_Current"] = call =>
            call.Interface == typeof(IPermissionRegistryProvider) ? REGISTRY : Fakes.NotHandled;
        _fakes.Handlers["GetSnapshotAsync"] = _ =>
            Task.FromResult(
                new PermissionGroupDirectorySnapshot
                {
                    Version = 1,
                    Groups = new[]
                    {
                        Group(1, PermissionGroupNames.DEFAULT, 0),
                        Group(2, "helper", 30),
                        Group(3, "moderator", 50),
                        Group(4, "manager", 70),
                        Group(5, "admin", 100),
                    }.ToImmutableDictionary(x => x.Id),
                }
            );
        _fakes.Handlers["GetResolvedAsync"] = call =>
            Task.FromResult(
                ResolvedPermissionsSnapshot.EMPTY with
                {
                    Granted = [.. _granted[Convert.ToInt64(call.Key)]],
                }
            );
    }

    [Fact]
    public async Task WithoutTheManageNodeNothingCanBeChanged()
    {
        var moderator = await EditorFor(MODERATOR);

        moderator.CheckGroup("helper").Should().Be(PermissionEditRefusal.NeedsManageNode);
        moderator.CheckWeight(1).Should().Be(PermissionEditRefusal.NeedsManageNode);
        (await moderator.CheckPlayerAsync(HELPER, Ct))
            .Should()
            .Be(PermissionEditRefusal.NeedsManageNode);
    }

    [Fact]
    public async Task OnlyGroupsLighterThanTheEditorsHeaviestAreTheirs()
    {
        var manager = await EditorFor(MANAGER);

        manager.HeaviestWeight.Should().Be(70);
        manager.CheckGroup("moderator").Should().Be(PermissionEditRefusal.None);
        manager.CheckGroup(PermissionGroupNames.DEFAULT).Should().Be(PermissionEditRefusal.None);
        // Their own group would let them give themselves anything: it is the console's.
        manager.CheckGroup("manager").Should().Be(PermissionEditRefusal.GroupTooHeavy);
        manager.CheckGroup("admin").Should().Be(PermissionEditRefusal.GroupTooHeavy);
    }

    [Fact]
    public async Task ANewOrReweightedGroupMustStayLighterThanTheEditor()
    {
        var manager = await EditorFor(MANAGER);

        manager.CheckWeight(69).Should().Be(PermissionEditRefusal.None);
        manager.CheckWeight(70).Should().Be(PermissionEditRefusal.GroupTooHeavy);
        manager.CheckWeight(500).Should().Be(PermissionEditRefusal.GroupTooHeavy);
    }

    [Fact]
    public async Task OnlyPlayersLighterThanTheEditorAreTheirs()
    {
        var manager = await EditorFor(MANAGER);

        (await manager.CheckPlayerAsync(MODERATOR, Ct)).Should().Be(PermissionEditRefusal.None);
        (await manager.CheckPlayerAsync(OWNER, Ct))
            .Should()
            .Be(PermissionEditRefusal.PlayerTooHeavy);
        // Nobody edits themselves: a manager could otherwise lift their own denials.
        (await manager.CheckPlayerAsync(MANAGER, Ct))
            .Should()
            .Be(PermissionEditRefusal.PlayerTooHeavy);
    }

    [Fact]
    public async Task OnlyNodesTheEditorHoldsCanBeGrantedOrDenied()
    {
        var manager = await EditorFor(MANAGER);

        manager.CheckAssignment("room.enter.locked").Should().Be(PermissionEditRefusal.None);
        manager
            .CheckAssignment(PermissionNodes.Command.SHUTDOWN)
            .Should()
            .Be(PermissionEditRefusal.NodeNotHeld);
    }

    [Fact]
    public async Task AWildcardNeedsEveryNodeItCovers()
    {
        var manager = await EditorFor(MANAGER);

        manager.CheckAssignment("room.*").Should().Be(PermissionEditRefusal.None);
        manager.CheckAssignment("*").Should().Be(PermissionEditRefusal.NodeNotHeld);
        manager.CheckAssignment("command.*").Should().Be(PermissionEditRefusal.NodeNotHeld);
    }

    [Fact]
    public async Task TheOwnerHoldsEverythingButTheirOwnGroupStaysTheConsoles()
    {
        var owner = await EditorFor(OWNER);

        owner.CheckAssignment("*").Should().Be(PermissionEditRefusal.None);
        owner.CheckGroup("manager").Should().Be(PermissionEditRefusal.None);
        (await owner.CheckPlayerAsync(MANAGER, Ct)).Should().Be(PermissionEditRefusal.None);
        owner.CheckGroup("admin").Should().Be(PermissionEditRefusal.GroupTooHeavy);
    }

    private Task<PermissionEditor> EditorFor(PlayerId player) =>
        new PermissionEditPolicy(
            _fakes.Create<IGrainFactory>(),
            _fakes.Create<IPermissionRegistryProvider>()
        ).ForAsync(player, Ct);

    /// <summary>The membership nodes of the groups a player reaches; default is reached by everyone.</summary>
    private static string[] Memberships(params string[] groups) =>
        [.. groups.Append(PermissionGroupNames.DEFAULT).Select(PermissionGroupNames.ToNode)];

    private static PermissionGroupSnapshot Group(int id, string name, int weight) =>
        new()
        {
            Id = id,
            Name = name,
            DisplayName = name,
            Weight = weight,
            ParentIds = [],
            Nodes = [],
            Meta = [],
        };
}
