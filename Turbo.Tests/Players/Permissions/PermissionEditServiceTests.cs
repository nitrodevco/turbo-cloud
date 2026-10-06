using System.Collections.Immutable;
using FluentAssertions;
using Orleans;
using Turbo.Players.Permissions;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players.Permissions;

/// <summary>
/// What a staff member may change, in the admin panel or with <c>:group</c>: with
/// <c>permissions.manage</c>, only groups and players lighter than their heaviest group, and only
/// nodes they hold themselves, as <c>docs/permissions.md</c> asks of any editor beyond the console.
/// The console may change anything.
/// </summary>
public sealed class PermissionEditServiceTests
{
    private static readonly PlayerId OWNER = new(1);
    private static readonly PlayerId MANAGER = new(2);
    private static readonly PlayerId MODERATOR = new(3);
    private static readonly PlayerId HELPER = new(4);
    private static readonly PlayerId SUPERUSER = new(5);

    private static readonly PermissionRegistry REGISTRY = new([new CorePermissionNodeSource()]);

    private static readonly string[] ROOM_NODES =
    [
        .. REGISTRY.Nodes.Keys.Where(x => x.StartsWith("room.", StringComparison.Ordinal)),
    ];

    private readonly Fakes _fakes = new();

    private readonly Dictionary<long, string[]> _granted = new()
    {
        // The owner's group holds `*`: every registered node a wildcard reaches, which leaves
        // out permissions.superuser.
        [OWNER.Value] =
        [
            .. REGISTRY.Nodes.Values.Where(x => !x.ExplicitOnly).Select(x => x.Node),
            .. Memberships("admin", "manager", "moderator"),
        ],
        // Lighter than the manager, but given the superuser node on purpose.
        [SUPERUSER.Value] =
        [
            PermissionNodes.Permissions.MANAGE,
            PermissionNodes.Permissions.SUPERUSER,
            .. Memberships("ops", "helper"),
        ],
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

    /// <summary>What the superuser really holds: manage of their own, and superuser through `ops`.</summary>
    private PlayerPermissionAssignmentsSnapshot _superuserAssignments = new()
    {
        Groups = [new PermissionGroupMembershipSnapshot { GroupId = 6 }],
        Nodes =
        [
            new PermissionNodeAssignmentSnapshot
            {
                Node = PermissionNodes.Permissions.MANAGE,
                Value = true,
            },
        ],
        Meta = [],
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public PermissionEditServiceTests()
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
                        // Light, but it gives the superuser node; "deputies" inherits it.
                        Group(6, "ops", 20, nodes: [PermissionNodes.Permissions.SUPERUSER]),
                        Group(7, "deputies", 25, parents: [6]),
                    }.ToImmutableDictionary(x => x.Id),
                }
            );
        _fakes.Handlers["GetAssignmentsAsync"] = _ => Task.FromResult(_superuserAssignments);
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

    [Fact]
    public async Task AWildcardDoesNotAskForTheSuperuserNode()
    {
        var owner = await EditorFor(OWNER);

        owner.IsSuperuser.Should().BeFalse();
        owner.CheckAssignment("*").Should().Be(PermissionEditRefusal.None);
        owner.CheckAssignment("permissions.*").Should().Be(PermissionEditRefusal.None);
        // Naming it is another matter: the owner does not hold it.
        owner
            .CheckAssignment(PermissionNodes.Permissions.SUPERUSER)
            .Should()
            .Be(PermissionEditRefusal.NodeNotHeld);
    }

    [Fact]
    public async Task AGroupThatGivesSuperuser_IsOnlyForASuperuser_DirectlyOrByInheritance()
    {
        var manager = await EditorFor(MANAGER);

        manager.CheckGroup("ops").Should().Be(PermissionEditRefusal.NeedsSuperuser);
        manager.CheckGroup("deputies").Should().Be(PermissionEditRefusal.NeedsSuperuser);
        manager.CheckGroup("moderator").Should().Be(PermissionEditRefusal.None);
    }

    [Fact]
    public async Task ASuperuserIsBoundByNoWeightAndNoHeldNode_ButStillNeedsTheManageNode()
    {
        var superuser = await EditorFor(SUPERUSER);

        superuser.IsSuperuser.Should().BeTrue();
        superuser.CheckGroup("admin").Should().Be(PermissionEditRefusal.None);
        superuser.CheckGroup("ops").Should().Be(PermissionEditRefusal.None);
        superuser.CheckWeight(1000).Should().Be(PermissionEditRefusal.None);
        superuser.CheckAssignment("*").Should().Be(PermissionEditRefusal.None);
        superuser
            .CheckAssignment(PermissionNodes.Command.SHUTDOWN)
            .Should()
            .Be(PermissionEditRefusal.None);
        (await superuser.CheckPlayerAsync(OWNER, Ct)).Should().Be(PermissionEditRefusal.None);
        (await superuser.CheckPlayerAsync(SUPERUSER, Ct)).Should().Be(PermissionEditRefusal.None);

        // Held without the manage node, the superuser node does nothing.
        _granted[HELPER.Value] = [PermissionNodes.Permissions.SUPERUSER, .. Memberships("helper")];

        var helper = await EditorFor(HELPER);

        helper.IsSuperuser.Should().BeFalse();
        helper.CheckGroup("admin").Should().Be(PermissionEditRefusal.NeedsManageNode);
    }

    [Fact]
    public async Task AManagerCannotHandOutSuperuserThroughALighterGroup()
    {
        var change = await Service()
            .AddToGroupAsync(MANAGER, HELPER, "ops", null, PermissionExpiryModeType.Replace, Ct);

        change.Refusal.Should().Be(PermissionEditRefusal.NeedsSuperuser);
        GroupCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task ASuperuserCannotTakeSuperuserFromThemselves_ByLeavingTheGroupThatGivesIt()
    {
        var change = await Service().RemoveFromGroupAsync(SUPERUSER, SUPERUSER, "ops", null, Ct);

        change.Refusal.Should().Be(PermissionEditRefusal.WouldLoseSuperuser);
        GroupCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task ASuperuserMayTakeSuperuserFromSomeoneElse()
    {
        _fakes.Handlers["RemoveGroupAsync"] = _ =>
            Task.FromResult(PermissionChangeResultType.Changed);

        var change = await Service().RemoveFromGroupAsync(SUPERUSER, HELPER, "ops", null, Ct);

        change.Refusal.Should().Be(PermissionEditRefusal.None);
        GroupCalls().Should().ContainSingle();
    }

    [Fact]
    public async Task ASuperuserCannotChangeTheGroupThatGivesThemSuperuserOutFromUnderThemselves()
    {
        var superuser = await EditorFor(SUPERUSER);
        const string NODE = PermissionNodes.Permissions.SUPERUSER;

        async Task<PermissionEditRefusal> Check(PermissionChange change) =>
            await superuser.CheckKeepsAccessAsync(change, Ct);

        (await Check(PermissionChange.GroupDeleted("ops")))
            .Should()
            .Be(PermissionEditRefusal.WouldLoseSuperuser);
        (await Check(PermissionChange.GroupNodeUnset("ops", NODE, false)))
            .Should()
            .Be(PermissionEditRefusal.WouldLoseSuperuser);
        (await Check(PermissionChange.GroupNodeSet("ops", NODE, false, null)))
            .Should()
            .Be(PermissionEditRefusal.WouldLoseSuperuser);

        // A temporary denial counts too: it wins while it lasts.
        (await Check(PermissionChange.GroupNodeSet("ops", NODE, false, START.AddDays(1))))
            .Should()
            .Be(PermissionEditRefusal.WouldLoseSuperuser);
        // Taking away what makes them able to manage is the same.
        (
            await Check(
                PermissionChange.GroupNodeSet(
                    "ops",
                    PermissionNodes.Permissions.MANAGE,
                    false,
                    null
                )
            )
        )
            .Should()
            .Be(PermissionEditRefusal.None, "the player's own manage node outranks a group's");
    }

    [Fact]
    public async Task ASuperuserMayChangeAnythingThatLeavesThemOne()
    {
        var superuser = await EditorFor(SUPERUSER);

        async Task<PermissionEditRefusal> Check(PermissionChange change) =>
            await superuser.CheckKeepsAccessAsync(change, Ct);

        (await Check(PermissionChange.GroupDeleted("helper")))
            .Should()
            .Be(PermissionEditRefusal.None);
        (await Check(PermissionChange.GroupWeightSet("ops", 500)))
            .Should()
            .Be(PermissionEditRefusal.None);
        (await Check(PermissionChange.GroupNodeUnset("ops", "room.enter.locked", false)))
            .Should()
            .Be(PermissionEditRefusal.None);
        (await Check(PermissionChange.GroupParentAdded("ops", "helper")))
            .Should()
            .Be(PermissionEditRefusal.None);
        // The group goes, but they hold the node of their own too.
        _superuserAssignments = _superuserAssignments with
        {
            Nodes =
            [
                .. _superuserAssignments.Nodes,
                new PermissionNodeAssignmentSnapshot
                {
                    Node = PermissionNodes.Permissions.SUPERUSER,
                    Value = true,
                },
            ],
        };
        (await Check(PermissionChange.GroupDeleted("ops"))).Should().Be(PermissionEditRefusal.None);
    }

    [Fact]
    public async Task APlayerWhoIsNotASuperuser_IsNotAskedToKeepIt()
    {
        var manager = await EditorFor(MANAGER);

        (await manager.CheckKeepsAccessAsync(PermissionChange.GroupDeleted("ops"), Ct))
            .Should()
            .Be(PermissionEditRefusal.None);
    }

    [Fact]
    public async Task TheConsoleIsNeverAskedToKeepIt()
    {
        var console = await Service().EditorForAsync(null, Ct);

        (await console.CheckKeepsAccessAsync(PermissionChange.GroupDeleted("ops"), Ct))
            .Should()
            .Be(PermissionEditRefusal.None);
    }

    [Fact]
    public async Task TheConsoleMayChangeAnything()
    {
        var console = await Service().EditorForAsync(null, Ct);

        console.CheckGroup("admin").Should().Be(PermissionEditRefusal.None);
        console.CheckWeight(1000).Should().Be(PermissionEditRefusal.None);
        console.CheckAssignment("*").Should().Be(PermissionEditRefusal.None);
        (await console.CheckPlayerAsync(OWNER, Ct)).Should().Be(PermissionEditRefusal.None);
    }

    [Fact]
    public async Task AGroupHeavierThanTheEditor_IsNotHandedOut()
    {
        var change = await Service()
            .AddToGroupAsync(MANAGER, HELPER, "admin", null, PermissionExpiryModeType.Replace, Ct);

        change.Refusal.Should().Be(PermissionEditRefusal.GroupTooHeavy);
        change.Notice.Should().BeNull();
        GroupCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task APlayerAsHeavyAsTheEditor_IsLeftAlone()
    {
        var change = await Service().RemoveFromGroupAsync(MANAGER, OWNER, "moderator", null, Ct);

        change.Refusal.Should().Be(PermissionEditRefusal.PlayerTooHeavy);
        GroupCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task ALighterGroupForALighterPlayer_IsGiven_AndThePlayerIsToldForHowLong()
    {
        _fakes.Handlers["AddGroupAsync"] = _ => Task.FromResult(PermissionChangeResultType.Changed);

        var change = await Service()
            .AddToGroupAsync(
                MANAGER,
                HELPER,
                "moderator",
                TimeSpan.FromDays(7),
                PermissionExpiryModeType.Replace,
                Ct
            );

        change.Result.Should().Be(PermissionChangeResultType.Changed);
        var call = GroupCalls().Should().ContainSingle().Subject;
        call.Args[0].Should().Be("moderator");
        call.Args[1].Should().Be(START.AddDays(7));
        ((PlayerId?)call.Args[3]).Should().Be(MANAGER);
        change.Notice!.TextKey.Should().Be(PermissionEditService.ADDED_NOTICE);
        change.Notice.Parameters.Should().Equal("moderator", "7 d");
    }

    [Fact]
    public async Task ARemovalOfEitherKind_TriesThePermanentMembershipFirst()
    {
        _fakes.Handlers["RemoveGroupAsync"] = call =>
            Task.FromResult(
                (bool)call.Args[1]!
                    ? PermissionChangeResultType.Changed
                    : PermissionChangeResultType.NotFound
            );

        var change = await Service().RemoveFromGroupAsync(MANAGER, HELPER, "helper", null, Ct);

        change.Result.Should().Be(PermissionChangeResultType.Changed);
        GroupCalls().Select(x => (bool)x.Args[1]!).Should().Equal(false, true);
        change.Notice!.TextKey.Should().Be(PermissionEditService.REMOVED_NOTICE);
    }

    private static readonly DateTime START = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    private PermissionEditService Service() =>
        new(
            _fakes.Create<IGrainFactory>(),
            _fakes.Create<IPermissionRegistryProvider>(),
            new ManualTimeProvider(START)
        );

    private Task<PermissionEditor> EditorFor(PlayerId player) =>
        Service().EditorForAsync(player, Ct);

    private IEnumerable<FakeCall> GroupCalls() =>
        _fakes
            .Log.On<IPlayerPermissionGrain>()
            .Where(x => x.Method is "AddGroupAsync" or "RemoveGroupAsync");

    /// <summary>The membership nodes of the groups a player reaches; default is reached by everyone.</summary>
    private static string[] Memberships(params string[] groups) =>
        [.. groups.Append(PermissionGroupNames.DEFAULT).Select(PermissionGroupNames.ToNode)];

    private static PermissionGroupSnapshot Group(
        int id,
        string name,
        int weight,
        string[]? nodes = null,
        int[]? parents = null
    ) =>
        new()
        {
            Id = id,
            Name = name,
            DisplayName = name,
            Weight = weight,
            ParentIds = [.. parents ?? []],
            Nodes =
            [
                .. (nodes ?? []).Select(x => new PermissionNodeAssignmentSnapshot
                {
                    Node = x,
                    Value = true,
                }),
            ],
            Meta = [],
        };
}
