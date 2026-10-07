using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;
using Xunit;

namespace Turbo.Tests.Players.Permissions;

public class PermissionResolverTests
{
    private static readonly DateTime NOW = new(2026, 9, 28, 12, 0, 0, DateTimeKind.Utc);

    private static readonly PermissionRegistry REGISTRY = new([
        new CorePermissionNodeSource(),
        new TestMetaSource(),
    ]);

    /// <summary>
    /// What a player holds with nothing assigned: the nodes core registers as granted by default
    /// (the chat commands everybody may use), and nothing else.
    /// </summary>
    private static readonly string[] DEFAULT_GRANTED =
    [
        .. REGISTRY.Nodes.Values.Where(x => x.GrantedByDefault).Select(x => x.Node),
    ];

    private const string LOCKED = PermissionNodes.Room.ENTER_LOCKED;
    private const string FULL = PermissionNodes.Room.ENTER_FULL;
    private const string FIRST = "test.first";
    private const string HIGH = "test.high";
    private const string LOW = "test.low";

    /// <summary>One meta key per selection, so each rule is tested on its own.</summary>
    private sealed class TestMetaSource : IPermissionNodeSource
    {
        public string? Prefix => "test";

        public IEnumerable<PermissionNodeDefinition> Nodes => [];

        public IEnumerable<PermissionMetaDefinition> MetaKeys =>
            [
                new(FIRST, "test"),
                new(HIGH, "test", PermissionMetaSelectionType.HighestNumber),
                new(LOW, "test", PermissionMetaSelectionType.LowestNumber),
            ];
    }

    // --- denial and defaults ---

    [Fact]
    public void NothingAssigned_GrantsOnlyWhatIsGrantedByDefault()
    {
        var resolved = Resolve([], PlayerPermissionAssignmentsSnapshot.EMPTY);

        resolved.Granted.Should().BeEquivalentTo(DEFAULT_GRANTED);
        DEFAULT_GRANTED.Should().NotBeEmpty();
        resolved.Meta.Should().BeEmpty();
        resolved.NextExpiresAt.Should().BeNull();
    }

    [Fact]
    public void DefaultGroup_IsHeldWithoutMembership()
    {
        var resolved = Resolve(
            [Group(1, PermissionGroupNames.DEFAULT, 0, nodes: [Node(PermissionNodes.TRADE)])],
            PlayerPermissionAssignmentsSnapshot.EMPTY
        );

        resolved.Has(PermissionNodes.TRADE).Should().BeTrue();
    }

    [Fact]
    public void MembershipOfMissingGroup_IsIgnored()
    {
        var resolved = Resolve([], Player(groups: [Member(99)]));

        resolved.Granted.Should().BeEquivalentTo(DEFAULT_GRANTED);
    }

    // --- source priority ---

    [Fact]
    public void PlayerDenial_BeatsGroupGrant()
    {
        var resolved = Resolve(
            [Group(1, "moderator", 50, nodes: [Node(LOCKED)])],
            Player(groups: [Member(1)], nodes: [Node(LOCKED, false)])
        );

        resolved.Has(LOCKED).Should().BeFalse();
    }

    [Fact]
    public void PlayerGrant_BeatsGroupDenial()
    {
        var resolved = Resolve(
            [Group(1, "sanctioned", 50, nodes: [Node(PermissionNodes.TRADE, false)])],
            Player(groups: [Member(1)], nodes: [Node(PermissionNodes.TRADE)])
        );

        resolved.Has(PermissionNodes.TRADE).Should().BeTrue();
    }

    [Fact]
    public void PlayerWildcard_BeatsGroupExactNode()
    {
        // Source order comes before specificity: the player's broad denial wins.
        var resolved = Resolve(
            [Group(1, "moderator", 50, nodes: [Node(LOCKED)])],
            Player(groups: [Member(1)], nodes: [Node("room.*", false)])
        );

        resolved.Has(LOCKED).Should().BeFalse();
    }

    [Fact]
    public void HigherWeightGroup_Wins()
    {
        var groups = new[]
        {
            Group(1, "vip", 10, nodes: [Node(LOCKED)]),
            Group(2, "sanctioned", 90, nodes: [Node(LOCKED, false)]),
        };

        var resolved = Resolve(groups, Player(groups: [Member(1), Member(2)]));

        resolved.Has(LOCKED).Should().BeFalse();
    }

    [Fact]
    public void EqualWeight_BreaksTieByName()
    {
        var groups = new[]
        {
            Group(1, "beta", 10, nodes: [Node(LOCKED, false)]),
            Group(2, "alpha", 10, nodes: [Node(LOCKED)]),
        };

        var resolved = Resolve(groups, Player(groups: [Member(1), Member(2)]));

        resolved.Has(LOCKED).Should().BeTrue();
    }

    // --- specificity within a source ---

    [Fact]
    public void ExactNode_BeatsWildcardInSameSource()
    {
        var resolved = Resolve(
            [Group(1, "moderator", 50, nodes: [Node("room.*"), Node(LOCKED, false)])],
            Player(groups: [Member(1)])
        );

        resolved.Has(LOCKED).Should().BeFalse();
        resolved.Has(FULL).Should().BeTrue();
    }

    [Fact]
    public void LongerWildcard_BeatsShorter()
    {
        var resolved = Resolve(
            [Group(1, "moderator", 50, nodes: [Node("room.*", false), Node("room.enter.*")])],
            Player(groups: [Member(1)])
        );

        resolved.Has(LOCKED).Should().BeTrue();
        resolved.Has(PermissionNodes.Room.CONTROL_ANY).Should().BeFalse();
    }

    [Fact]
    public void Denial_BeatsGrant_AtEqualSpecificity()
    {
        var resolved = Resolve(
            [Group(1, "confused", 50, nodes: [Node("room.*"), Node("room.*", false)])],
            Player(groups: [Member(1)])
        );

        resolved.Has(LOCKED).Should().BeFalse();
    }

    [Fact]
    public void Star_GrantsEveryRegisteredNodeButTheExplicitOnlyOnes()
    {
        var resolved = Resolve(
            [Group(1, "admin", 100, nodes: [Node(PermissionNodeFormat.WILDCARD)])],
            Player(groups: [Member(1)])
        );

        // The wildcard reaches registered nodes only; the membership node comes from holding admin.
        resolved
            .Granted.Should()
            .BeEquivalentTo(
                REGISTRY
                    .Nodes.Values.Where(x => !x.ExplicitOnly)
                    .Select(x => x.Node)
                    .Append("group.admin")
            );
    }

    // --- inheritance ---

    [Fact]
    public void Group_InheritsFromParents()
    {
        var groups = new[]
        {
            Group(1, "helper", 30, nodes: [Node(PermissionNodes.Perk.GUIDE_TOOL)]),
            Group(2, "moderator", 50, parents: [1], nodes: [Node(LOCKED)]),
        };

        var resolved = Resolve(groups, Player(groups: [Member(2)]));

        resolved.Has(LOCKED).Should().BeTrue();
        resolved.Has(PermissionNodes.Perk.GUIDE_TOOL).Should().BeTrue();
    }

    [Fact]
    public void InheritedGroup_KeepsItsOwnWeight()
    {
        // The heavier parent's denial beats the lighter child's grant: weight, not depth, orders groups.
        var groups = new[]
        {
            Group(1, "restricted", 90, nodes: [Node(LOCKED, false)]),
            Group(2, "vip", 10, parents: [1], nodes: [Node(LOCKED)]),
        };

        var resolved = Resolve(groups, Player(groups: [Member(2)]));

        resolved.Has(LOCKED).Should().BeFalse();
    }

    [Fact]
    public void Diamond_CountsSharedAncestorOnce()
    {
        var groups = new[]
        {
            Group(1, "base", 0, nodes: [Node(LOCKED)]),
            Group(2, "left", 10, parents: [1]),
            Group(3, "right", 20, parents: [1]),
            Group(4, "top", 30, parents: [2, 3]),
        };

        var check = Explain(groups, Player(groups: [Member(4)]), LOCKED);

        check.Granted.Should().BeTrue();
        check.Overridden.Should().BeEmpty();
        check.Decision!.Path.Should().Equal("top", "left", "base");
    }

    [Fact]
    public void Cycle_Terminates()
    {
        var groups = new[]
        {
            Group(1, "a", 10, parents: [2], nodes: [Node(LOCKED)]),
            Group(2, "b", 20, parents: [1], nodes: [Node(FULL)]),
        };

        var resolved = Resolve(groups, Player(groups: [Member(1)]));

        resolved.Has(LOCKED).Should().BeTrue();
        resolved.Has(FULL).Should().BeTrue();
    }

    // --- expiry ---

    [Fact]
    public void ExpiredMembership_IsIgnored()
    {
        var resolved = Resolve(
            [Group(1, "vip", 10, nodes: [Node(LOCKED)])],
            Player(groups: [Member(1, NOW)])
        );

        resolved.Has(LOCKED).Should().BeFalse();
    }

    [Fact]
    public void ExpiredPlayerDenial_StopsMasking()
    {
        var resolved = Resolve(
            [Group(1, PermissionGroupNames.DEFAULT, 0, nodes: [Node(PermissionNodes.TRADE)])],
            Player(nodes: [Node(PermissionNodes.TRADE, false, NOW.AddSeconds(-1))])
        );

        resolved.Has(PermissionNodes.TRADE).Should().BeTrue();
    }

    [Fact]
    public void ExpiredGroupNode_IsIgnored()
    {
        var resolved = Resolve(
            [Group(1, "vip", 10, nodes: [Node(LOCKED, true, NOW.AddDays(-1))])],
            Player(groups: [Member(1)])
        );

        resolved.Has(LOCKED).Should().BeFalse();
    }

    [Fact]
    public void NextExpiresAt_IsEarliestLiveExpiry()
    {
        var soon = NOW.AddHours(1);
        var later = NOW.AddDays(7);

        var resolved = Resolve(
            [Group(1, "vip", 10, nodes: [Node(LOCKED, true, soon)])],
            Player(
                groups: [Member(1, later)],
                nodes: [Node(PermissionNodes.TRADE, false, NOW.AddDays(-1))]
            )
        );

        resolved.NextExpiresAt.Should().Be(soon);
    }

    [Fact]
    public void NextExpiresAt_IgnoresGroupsNotHeld()
    {
        var resolved = Resolve(
            [Group(1, "vip", 10, nodes: [Node(LOCKED, true, NOW.AddHours(1))])],
            PlayerPermissionAssignmentsSnapshot.EMPTY
        );

        resolved.NextExpiresAt.Should().BeNull();
    }

    // --- meta ---

    [Fact]
    public void Meta_PlayerBeatsGroup_HeavierGroupBeatsLighter()
    {
        var groups = new[]
        {
            Group(1, "helper", 30, meta: [Meta(FIRST, "2")]),
            Group(2, "moderator", 50, meta: [Meta(FIRST, "5")]),
        };

        Resolve(groups, Player(groups: [Member(1), Member(2)])).Meta[FIRST].Should().Be("5");

        Resolve(groups, Player(groups: [Member(1), Member(2)], meta: [Meta(FIRST, "7")]))
            .Meta[FIRST]
            .Should()
            .Be("7");
    }

    [Fact]
    public void ExpiredMeta_FallsThrough()
    {
        var resolved = Resolve(
            [Group(1, "moderator", 50, meta: [Meta(FIRST, "5")])],
            Player(groups: [Member(1)], meta: [Meta(FIRST, "7", NOW)])
        );

        resolved.Meta[FIRST].Should().Be("5");
    }

    [Fact]
    public void HighestNumber_TakesLargestAcrossSources()
    {
        // The lighter group's larger limit wins; inheritance order would have given 50.
        var groups = new[]
        {
            Group(1, "vip", 10, meta: [Meta(HIGH, "50")]),
            Group(2, "builder", 5, meta: [Meta(HIGH, "200"), Meta(LOW, "3")]),
        };

        var resolved = Resolve(
            groups,
            Player(groups: [Member(1), Member(2)], meta: [Meta(HIGH, "lots"), Meta(LOW, "9")])
        );

        resolved.Meta[HIGH].Should().Be("200");
        resolved.Meta[LOW].Should().Be("3");
    }

    [Fact]
    public void NumericSelection_WithNoNumbers_SetsNothing()
    {
        var resolved = Resolve([], Player(meta: [Meta(HIGH, "lots")]));

        resolved.Meta.Should().NotContainKey(HIGH);
    }

    [Fact]
    public void Inheritance_TemporaryMetaBeatsPermanent_InSameSource()
    {
        var resolved = Resolve(
            [],
            Player(meta: [Meta(FIRST, "permanent"), Meta(FIRST, "temporary", NOW.AddHours(1))])
        );

        resolved.Meta[FIRST].Should().Be("temporary");
    }

    // --- temporary against permanent ---

    [Fact]
    public void TemporaryDenial_SuspendsPermanentGrant_ThenItReturns()
    {
        var player = Player(
            nodes: [Node(PermissionNodes.TRADE), Node(PermissionNodes.TRADE, false, NOW.AddDays(7))]
        );

        Resolve([], player).Has(PermissionNodes.TRADE).Should().BeFalse();

        PermissionResolver
            .Resolve(REGISTRY, ById([]), player, NOW.AddDays(8))
            .Has(PermissionNodes.TRADE)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void TemporaryGrant_BeatsPermanentDenial()
    {
        var resolved = Resolve(
            [],
            Player(nodes: [Node(LOCKED, false), Node(LOCKED, true, NOW.AddHours(1))])
        );

        resolved.Has(LOCKED).Should().BeTrue();
    }

    [Fact]
    public void PermanentExactNode_BeatsTemporaryWildcard()
    {
        // Specificity is weighed before temporariness, as in LuckPerms.
        var resolved = Resolve(
            [],
            Player(nodes: [Node(LOCKED), Node("room.*", false, NOW.AddHours(1))])
        );

        resolved.Has(LOCKED).Should().BeTrue();
        resolved.Has(FULL).Should().BeFalse();
    }

    // --- unregistered ---

    [Fact]
    public void UnregisteredAssignments_AreReported_NotGranted()
    {
        var resolved = Resolve(
            [
                Group(
                    1,
                    "vip",
                    10,
                    nodes: [Node("casino.table.open"), Node("casino.*")],
                    meta: [Meta("casino.limit.tables", "3")]
                ),
            ],
            Player(groups: [Member(1)])
        );

        resolved.Granted.Should().BeEquivalentTo(["group.vip", .. DEFAULT_GRANTED]);
        resolved.UnregisteredNodes.Should().Equal("casino.table.open");
        resolved.UnregisteredMetaKeys.Should().Equal("casino.limit.tables");
        resolved.Meta.Should().BeEmpty();
    }

    // --- membership nodes ---

    [Fact]
    public void MembershipNodes_CoverHeldInheritedAndDefaultGroups()
    {
        var groups = new[]
        {
            Group(1, PermissionGroupNames.DEFAULT, 0),
            Group(2, "helper", 30),
            Group(3, "moderator", 50, parents: [2]),
            Group(4, "vip", 10),
        };

        var resolved = Resolve(groups, Player(groups: [Member(3)]));

        resolved.Has("group.default").Should().BeTrue();
        resolved.Has("group.moderator").Should().BeTrue();
        resolved.Has("group.helper").Should().BeTrue();
        resolved.Has("group.vip").Should().BeFalse();
    }

    [Fact]
    public void MembershipNode_GoesWithAnExpiredMembership()
    {
        var resolved = Resolve(
            [Group(1, "vip", 10)],
            Player(groups: [Member(1, NOW.AddMinutes(-1))])
        );

        resolved.Has("group.vip").Should().BeFalse();
    }

    [Fact]
    public void MembershipNode_CannotBeAssignedOrDenied()
    {
        var resolved = Resolve(
            [Group(1, "vip", 10), Group(2, "moderator", 50, nodes: [Node("*")])],
            Player(
                groups: [Member(1), Member(2)],
                nodes: [Node("group.vip", false), Node("group.admin")]
            )
        );

        resolved.Has("group.vip").Should().BeTrue();
        resolved.Has("group.admin").Should().BeFalse();
        resolved.UnregisteredNodes.Should().Equal("group.admin", "group.vip");
    }

    [Fact]
    public void Explain_MembershipNode_NamesThePathToTheGroup()
    {
        var groups = new[] { Group(2, "helper", 30), Group(3, "moderator", 50, parents: [2]) };

        var held = Explain(groups, Player(groups: [Member(3)]), "group.helper");
        var missing = Explain(groups, Player(groups: [Member(3)]), "group.vip");

        held.IsRegistered.Should().BeTrue();
        held.Granted.Should().BeTrue();
        held.Decision!.GroupName.Should().Be("helper");
        held.Decision.Path.Should().Equal("moderator", "helper");
        missing.IsRegistered.Should().BeTrue();
        missing.Granted.Should().BeFalse();
        missing.Decision.Should().BeNull();
    }

    // --- explain ---

    [Fact]
    public void Explain_NamesDecisionAndWhatItBeat()
    {
        var groups = new[]
        {
            Group(1, PermissionGroupNames.DEFAULT, 0, nodes: [Node("room.*", false)]),
            Group(2, "helper", 30),
            Group(3, "moderator", 50, parents: [2], nodes: [Node("room.enter.*")]),
        };

        var check = Explain(groups, Player(groups: [Member(3)]), LOCKED);

        check.IsRegistered.Should().BeTrue();
        check.Granted.Should().BeTrue();
        check.Decision!.SourceType.Should().Be(PermissionSourceType.Group);
        check.Decision.GroupName.Should().Be("moderator");
        check.Decision.Node.Should().Be("room.enter.*");
        check.Decision.Path.Should().Equal("moderator");
        check.Overridden.Should().ContainSingle();
        check.Overridden[0].GroupName.Should().Be(PermissionGroupNames.DEFAULT);
        check.Overridden[0].Value.Should().BeFalse();
    }

    [Fact]
    public void Explain_PlayerSource_HasEmptyPath()
    {
        var check = Explain([], Player(nodes: [Node(LOCKED)]), LOCKED);

        check.Decision!.SourceType.Should().Be(PermissionSourceType.Player);
        check.Decision.Path.Should().BeEmpty();
        check.Decision.GroupId.Should().BeNull();
    }

    [Fact]
    public void Explain_NoMatch_IsDeniedWithoutDecision()
    {
        var check = Explain([], PlayerPermissionAssignmentsSnapshot.EMPTY, "casino.table.open");

        check.IsRegistered.Should().BeFalse();
        check.Granted.Should().BeFalse();
        check.Decision.Should().BeNull();
        check.Overridden.Should().BeEmpty();
    }

    [Fact]
    public void Explain_AgreesWithResolve_ForEveryRegisteredNode()
    {
        var groups = new[]
        {
            Group(1, PermissionGroupNames.DEFAULT, 0, nodes: [Node("perk.*")]),
            Group(2, "moderator", 50, parents: [1], nodes: [Node("room.*"), Node(FULL, false)]),
        };
        var player = Player(groups: [Member(2)], nodes: [Node("perk.camera", false)]);

        var resolved = Resolve(groups, player);

        foreach (var node in REGISTRY.Nodes.Keys)
            Explain(groups, player, node).Granted.Should().Be(resolved.Has(node), node);
    }

    // --- granted by default ---

    private const string EVERYDAY = "plug.everyday";

    /// <summary>A plugin node every player holds unless something denies it.</summary>
    private sealed class DefaultGrantSource : IPermissionNodeSource
    {
        public string? Prefix => "plug";

        public IEnumerable<PermissionNodeDefinition> Nodes =>
            [new(EVERYDAY, "test", GrantedByDefault: true)];

        public IEnumerable<PermissionMetaDefinition> MetaKeys => [];
    }

    private static readonly PermissionRegistry DEFAULTS_REGISTRY = new([
        new CorePermissionNodeSource(),
        new DefaultGrantSource(),
    ]);

    [Fact]
    public void GrantedByDefault_IsHeldWithNothingAssigned()
    {
        var resolved = PermissionResolver.Resolve(
            DEFAULTS_REGISTRY,
            ById([]),
            PlayerPermissionAssignmentsSnapshot.EMPTY,
            NOW
        );

        resolved.Has(EVERYDAY).Should().BeTrue();
        resolved.Has(LOCKED).Should().BeFalse();
    }

    [Fact]
    public void GrantedByDefault_GroupDenialDecides()
    {
        var groups = new[]
        {
            Group(1, PermissionGroupNames.DEFAULT, 0, nodes: [Node(EVERYDAY, false)]),
        };

        var resolved = PermissionResolver.Resolve(
            DEFAULTS_REGISTRY,
            ById(groups),
            PlayerPermissionAssignmentsSnapshot.EMPTY,
            NOW
        );

        resolved.Has(EVERYDAY).Should().BeFalse();
    }

    [Fact]
    public void GrantedByDefault_WildcardDenialDecides()
    {
        var resolved = PermissionResolver.Resolve(
            DEFAULTS_REGISTRY,
            ById([]),
            Player(nodes: [Node("plug.*", false)]),
            NOW
        );

        resolved.Has(EVERYDAY).Should().BeFalse();
    }

    [Fact]
    public void GrantedByDefault_ExplainsWithoutADecision()
    {
        var check = PermissionResolver.Explain(
            DEFAULTS_REGISTRY,
            ById([]),
            PlayerPermissionAssignmentsSnapshot.EMPTY,
            EVERYDAY,
            NOW
        );

        check.Granted.Should().BeTrue();
        check.Decision.Should().BeNull();
    }

    [Fact]
    public void GrantedByDefault_TemporaryDenialRunsOut()
    {
        var sanction = Player(nodes: [Node(EVERYDAY, false, NOW.AddDays(7))]);

        PermissionResolver
            .Resolve(DEFAULTS_REGISTRY, ById([]), sanction, NOW)
            .Has(EVERYDAY)
            .Should()
            .BeFalse();
        PermissionResolver
            .Resolve(DEFAULTS_REGISTRY, ById([]), sanction, NOW.AddDays(8))
            .Has(EVERYDAY)
            .Should()
            .BeTrue();
    }

    // --- explicit-only nodes ---

    private const string SUPERUSER = PermissionNodes.Permissions.SUPERUSER;

    [Fact]
    public void ExplicitOnly_IsNotGrantedByAWildcard()
    {
        var resolved = Resolve(
            [Group(1, "admin", 100, nodes: [Node("*")])],
            Player(groups: [Member(1)], nodes: [Node("permissions.*")])
        );

        resolved.Has(PermissionNodes.Permissions.MANAGE).Should().BeTrue();
        resolved.Has(SUPERUSER).Should().BeFalse();
    }

    [Fact]
    public void ExplicitOnly_IsGrantedWhenNamed()
    {
        var byGroup = Resolve(
            [Group(1, "ops", 20, nodes: [Node(SUPERUSER)])],
            Player(groups: [Member(1)])
        );
        var byPlayer = Resolve([], Player(nodes: [Node(SUPERUSER)]));

        byGroup.Has(SUPERUSER).Should().BeTrue();
        byPlayer.Has(SUPERUSER).Should().BeTrue();
    }

    [Fact]
    public void ExplicitOnly_AWildcardDenialDoesNotTakeItAway()
    {
        var resolved = Resolve(
            [Group(1, "ops", 20, nodes: [Node(SUPERUSER)])],
            Player(groups: [Member(1)], nodes: [Node("*", false)])
        );

        resolved.Has(SUPERUSER).Should().BeTrue();
    }

    [Fact]
    public void ExplicitOnly_ANamedDenialStillDecides()
    {
        var resolved = Resolve(
            [Group(1, "ops", 20, nodes: [Node(SUPERUSER)])],
            Player(groups: [Member(1)], nodes: [Node(SUPERUSER, false)])
        );

        resolved.Has(SUPERUSER).Should().BeFalse();
    }

    [Fact]
    public void ExplicitOnly_ExplainsWithoutTheWildcard()
    {
        var check = Explain(
            [Group(1, "admin", 100, nodes: [Node("*")])],
            Player(groups: [Member(1)]),
            SUPERUSER
        );

        check.Granted.Should().BeFalse();
        check.Decision.Should().BeNull();
    }

    [Fact]
    public void GrantedUntil_IsWhenTheGrantStops_ThroughAnExpiringMembershipToo()
    {
        const string CLUB = PermissionNodes.Club.HABBO_CLUB_UNLIMITED;
        var membershipEnds = NOW.AddDays(30);
        var nodeEnds = NOW.AddDays(10);
        PermissionGroupSnapshot[] groups =
        [
            Group(1, "vip", 10, parents: [2]),
            Group(2, "club", 5, nodes: [Node(CLUB)]),
            Group(3, "staff", 50, parents: [2]),
        ];

        // Reached through a membership that runs out, through a parent: it ends with it.
        Explain(groups, Player(groups: [Member(1, membershipEnds)]), CLUB)
            .Decision!.GrantedUntil.Should()
            .Be(membershipEnds);

        // A permanent membership reaching the same group keeps it for ever.
        Explain(groups, Player(groups: [Member(1, membershipEnds), Member(3)]), CLUB)
            .Decision!.GrantedUntil.Should()
            .BeNull();

        // The node's own end, when it comes first.
        Explain([], Player(nodes: [Node(CLUB, expiresAt: nodeEnds)]), CLUB)
            .Decision!.GrantedUntil.Should()
            .Be(nodeEnds);
        Explain([], Player(nodes: [Node(CLUB)]), CLUB).Decision!.GrantedUntil.Should().BeNull();
    }

    // --- helpers ---

    private static ResolvedPermissionsSnapshot Resolve(
        IEnumerable<PermissionGroupSnapshot> groups,
        PlayerPermissionAssignmentsSnapshot player
    ) => PermissionResolver.Resolve(REGISTRY, ById(groups), player, NOW);

    private static PermissionCheckSnapshot Explain(
        IEnumerable<PermissionGroupSnapshot> groups,
        PlayerPermissionAssignmentsSnapshot player,
        string node
    ) => PermissionResolver.Explain(REGISTRY, ById(groups), player, node, NOW);

    private static Dictionary<int, PermissionGroupSnapshot> ById(
        IEnumerable<PermissionGroupSnapshot> groups
    ) => groups.ToDictionary(x => x.Id);

    private static PermissionGroupSnapshot Group(
        int id,
        string name,
        int weight,
        int[]? parents = null,
        PermissionNodeAssignmentSnapshot[]? nodes = null,
        PermissionMetaAssignmentSnapshot[]? meta = null
    ) =>
        new()
        {
            Id = id,
            Name = name,
            DisplayName = name,
            Weight = weight,
            ParentIds = [.. parents ?? []],
            Nodes = [.. nodes ?? []],
            Meta = [.. meta ?? []],
        };

    private static PlayerPermissionAssignmentsSnapshot Player(
        PermissionGroupMembershipSnapshot[]? groups = null,
        PermissionNodeAssignmentSnapshot[]? nodes = null,
        PermissionMetaAssignmentSnapshot[]? meta = null
    ) =>
        new()
        {
            Groups = [.. groups ?? []],
            Nodes = [.. nodes ?? []],
            Meta = [.. meta ?? []],
        };

    private static PermissionGroupMembershipSnapshot Member(
        int groupId,
        DateTime? expiresAt = null
    ) => new() { GroupId = groupId, ExpiresAt = expiresAt };

    private static PermissionNodeAssignmentSnapshot Node(
        string node,
        bool value = true,
        DateTime? expiresAt = null
    ) =>
        new()
        {
            Node = node,
            Value = value,
            ExpiresAt = expiresAt,
        };

    private static PermissionMetaAssignmentSnapshot Meta(
        string key,
        string value,
        DateTime? expiresAt = null
    ) =>
        new()
        {
            Key = key,
            Value = value,
            ExpiresAt = expiresAt,
        };
}
