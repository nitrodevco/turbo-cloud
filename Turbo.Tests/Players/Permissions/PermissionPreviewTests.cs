using System.Collections.Immutable;
using FluentAssertions;
using Turbo.Players.Grains.Permissions;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Grains;
using Turbo.Primitives.Players.Grains.Permissions;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Turbo.Tests.Support;
using Xunit;

namespace Turbo.Tests.Players.Permissions;

/// <summary>
/// Previewing a group (the admin panel's "View as"): for an hour, or until stopped, the player
/// holds what a plain member of a lighter group holds, so staff see the hotel and the panel as
/// that group does. It only ever takes powers away: a group as heavy as theirs, or one giving a
/// node they lack, is refused, and a preview the rule stops allowing ends. What they hold of
/// their own stays readable beside it, for the checks that protect them from others.
/// </summary>
public sealed class PermissionPreviewTests
{
    private static readonly PermissionRegistry REGISTRY = new([new CorePermissionNodeSource()]);

    private const int HELPER = 2;
    private const int MODERATOR = 3;
    private const int ADMIN = 4;
    private const int PICKERS = 5;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed class Clock : TimeProvider
    {
        public DateTime Now { get; set; } = new(2026, 11, 26, 12, 0, 0, DateTimeKind.Utc);

        public override DateTimeOffset GetUtcNow() => new(Now);
    }

    /// <summary>The real permission grain of one player, over the groups below.</summary>
    private sealed class Player
    {
        private readonly object _grain;
        private readonly PlayerPermissionLiveState _state;

        public Player(params int[] groups)
        {
            Fakes.Handlers["get_Current"] = _ => REGISTRY;
            _grain = GrainHarness.Create<PlayerPermissionGrain>(Fakes, new InMemoryDb());
            GrainHarness.Replace(_grain, Fakes.Create<IPermissionRegistryProvider>());
            GrainHarness.Replace<TimeProvider>(_grain, Clock);
            GrainHarness.Replace(_grain, Fakes.Create<IPlayerNoticeService>());
            _state = GrainHarness.Get<PlayerPermissionLiveState>(_grain);
            _state.Groups = Directory(1, Groups());

            foreach (var group in groups)
                _state.MembershipsByGroupId[(group, false)] = new() { GroupId = group };
        }

        public Fakes Fakes { get; } = new();

        public Clock Clock { get; } = new();

        public IPlayerPermissionGrain Grain => (IPlayerPermissionGrain)_grain;

        public void Holds(string node) =>
            _state.NodesByNode[(node, false)] = new() { Node = node, Value = true };

        /// <summary>The notices the player was sent, by text key.</summary>
        public IEnumerable<string> Notices =>
            Fakes
                .Log.On<IPlayerNoticeService>()
                .Where(x => x.Method == nameof(IPlayerNoticeService.SendAsync))
                .Select(x => (string)x.Args[1]!);

        /// <summary>The resolved sets the player's room was told of, in order.</summary>
        public IEnumerable<ResolvedPermissionsSnapshot> RoomCopies =>
            Fakes
                .Log.On<IPlayerPresenceGrain>()
                .Where(x => x.Method == nameof(IPlayerPresenceGrain.OnPermissionsChangedAsync))
                .Select(x => (ResolvedPermissionsSnapshot)x.Args[0]!);
    }

    private static ImmutableArray<PermissionGroupSnapshot> Groups(int helperWeight = 30) =>
        [
            Group(1, PermissionGroupNames.DEFAULT, 0),
            Group(HELPER, "helper", helperWeight, PermissionNodes.Moderation.TOOL),
            Group(MODERATOR, "moderator", 50, "room.*", PermissionNodes.Moderation.TOOL),
            Group(ADMIN, "admin", 100, "*"),
            // Light, but it gives a node the moderators do not hold.
            Group(PICKERS, "pickers", 20, PermissionNodes.Navigator.STAFF_PICK),
        ];

    private static PermissionGroupDirectorySnapshot Directory(
        int version,
        ImmutableArray<PermissionGroupSnapshot> groups
    ) => new() { Version = version, Groups = groups.ToImmutableDictionary(x => x.Id) };

    private static PermissionGroupSnapshot Group(
        int id,
        string name,
        int weight,
        params string[] nodes
    ) =>
        new()
        {
            Id = id,
            Name = name,
            DisplayName = name,
            Weight = weight,
            ParentIds = [],
            Nodes =
            [
                .. nodes.Select(x => new PermissionNodeAssignmentSnapshot
                {
                    Node = x,
                    Value = true,
                }),
            ],
            Meta = [],
        };

    [Fact]
    public async Task APreview_HoldsWhatAPlainMemberHolds_UntilItIsStopped()
    {
        var admin = new Player(ADMIN);
        var level = (await admin.Grain.GetClientStateAsync(Ct)).SecurityLevel;

        var started = await admin.Grain.StartPreviewAsync("helper", Ct);

        started.Result.Should().Be(PermissionPreviewResultType.Started);
        started.Preview!.GroupName.Should().Be("helper");
        started.Preview.EndsAt.Should().Be(admin.Clock.Now.AddHours(1), "the shipped hour");
        (await admin.Grain.HasAsync(PermissionNodes.Room.CONTROL_ANY, Ct)).Should().BeFalse();
        (await admin.Grain.HasAsync(PermissionNodes.Moderation.TOOL, Ct)).Should().BeTrue();
        ((int)(await admin.Grain.GetClientStateAsync(Ct)).SecurityLevel)
            .Should()
            .BeLessThan((int)level);
        (await admin.Grain.ExplainAsync(PermissionNodes.Room.CONTROL_ANY, Ct))
            .Preview!.GroupName.Should()
            .Be("helper", "a check explains the answer the player gets now");
        admin
            .RoomCopies.Last()
            .Preview!.GroupName.Should()
            .Be("helper", "the room acts on the previewed set too");
        admin.Notices.Should().Equal("player.permissions.preview.started");

        // What they hold of their own is untouched, for the checks that protect them.
        var standing = await admin.Grain.GetStandingAsync(Ct);
        standing.Has(PermissionNodes.Room.CONTROL_ANY).Should().BeTrue();
        standing.Preview.Should().BeNull();

        (await admin.Grain.EndPreviewAsync(Ct)).Should().BeTrue();

        (await admin.Grain.HasAsync(PermissionNodes.Room.CONTROL_ANY, Ct)).Should().BeTrue();
        (await admin.Grain.GetResolvedAsync(Ct)).Preview.Should().BeNull();
        (await admin.Grain.GetClientStateAsync(Ct)).SecurityLevel.Should().Be(level);
        admin
            .Notices.Should()
            .Equal("player.permissions.preview.started", "player.permissions.preview.ended");
        (await admin.Grain.EndPreviewAsync(Ct)).Should().BeFalse("nothing is running any more");
    }

    [Fact]
    public async Task AGroupAsHeavyAsTheirOwn_OrHeavier_IsRefused()
    {
        var moderator = new Player(MODERATOR);

        (await moderator.Grain.StartPreviewAsync("admin", Ct))
            .Result.Should()
            .Be(PermissionPreviewResultType.GroupTooHeavy);
        (await moderator.Grain.StartPreviewAsync("moderator", Ct))
            .Result.Should()
            .Be(PermissionPreviewResultType.GroupTooHeavy);
        (await moderator.Grain.StartPreviewAsync("nobody", Ct))
            .Result.Should()
            .Be(PermissionPreviewResultType.UnknownGroup);
        (await moderator.Grain.GetResolvedAsync(Ct)).Preview.Should().BeNull();
        (await moderator.Grain.HasAsync(PermissionNodes.Room.CONTROL_ANY, Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task ALighterGroupGivingANodeTheyLack_IsRefused_AndNamesTheNode()
    {
        var moderator = new Player(MODERATOR);

        var refused = await moderator.Grain.StartPreviewAsync("pickers", Ct);

        refused.Result.Should().Be(PermissionPreviewResultType.NodeNotHeld);
        refused.Node.Should().Be(PermissionNodes.Navigator.STAFF_PICK);
        (await moderator.Grain.HasAsync(PermissionNodes.Navigator.STAFF_PICK, Ct))
            .Should()
            .BeFalse("a preview never gives more than the player holds");
    }

    [Fact]
    public async Task ASuperuser_MayPreviewAnyGroup()
    {
        var superuser = new Player(MODERATOR);
        superuser.Holds(PermissionNodes.Permissions.MANAGE);
        superuser.Holds(PermissionNodes.Permissions.SUPERUSER);

        (await superuser.Grain.StartPreviewAsync("admin", Ct))
            .Result.Should()
            .Be(PermissionPreviewResultType.Started);
        (await superuser.Grain.HasAsync(PermissionNodes.Room.CONTROL_ANY, Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task APreview_EndsByItself_WhenItsTimeIsUp()
    {
        var admin = new Player(ADMIN);
        await admin.Grain.StartPreviewAsync("helper", Ct);

        admin.Clock.Now = admin.Clock.Now.AddMinutes(61);

        (await admin.Grain.HasAsync(PermissionNodes.Room.CONTROL_ANY, Ct)).Should().BeTrue();
        (await admin.Grain.GetResolvedAsync(Ct)).Preview.Should().BeNull();
        admin.Notices.Should().EndWith("player.permissions.preview.ended");
    }

    [Fact]
    public async Task APreview_Ends_WhenItsGroupIsMadeAsHeavyAsThePlayer()
    {
        var moderator = new Player(MODERATOR);
        await moderator.Grain.StartPreviewAsync("helper", Ct);

        // Heavier than them, the helpers' group would let them edit the groups in between.
        await moderator.Grain.OnGroupsChangedAsync(Directory(2, Groups(helperWeight: 60)), Ct);

        (await moderator.Grain.GetResolvedAsync(Ct)).Preview.Should().BeNull();
        (await moderator.Grain.HasAsync(PermissionNodes.Room.CONTROL_ANY, Ct)).Should().BeTrue();
    }

    [Fact]
    public async Task APreview_Ends_WhenTheyLoseANodeItGives()
    {
        var admin = new Player(ADMIN);
        await admin.Grain.StartPreviewAsync("helper", Ct);

        // A sanction on the player themselves: the helpers' group still gives the node.
        (
            await admin.Grain.SetNodeAsync(
                PermissionNodes.Moderation.TOOL,
                false,
                null,
                PermissionExpiryModeType.Replace,
                null,
                Ct
            )
        )
            .Should()
            .Be(PermissionChangeResultType.Changed);

        (await admin.Grain.GetResolvedAsync(Ct)).Preview.Should().BeNull();
        (await admin.Grain.HasAsync(PermissionNodes.Moderation.TOOL, Ct)).Should().BeFalse();
    }
}
