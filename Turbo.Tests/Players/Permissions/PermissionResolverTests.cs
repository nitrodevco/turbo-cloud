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
    public void NothingAssigned_DeniesEverything()
    {
        var resolved = Resolve([], PlayerPermissionAssignmentsSnapshot.EMPTY);

        resolved.Granted.Should().BeEmpty();
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

        resolved.Granted.Should().BeEmpty();
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
    public void Star_GrantsEveryRegisteredNode()
    {
        var resolved = Resolve(
            [Group(1, "admin", 100, nodes: [Node(PermissionNodeFormat.WILDCARD)])],
            Player(groups: [Member(1)])
        );

        resolved.Granted.Should().BeEquivalentTo(REGISTRY.Nodes.Keys);
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

        resolved.Granted.Should().BeEmpty();
        resolved.UnregisteredNodes.Should().Equal("casino.table.open");
        resolved.UnregisteredMetaKeys.Should().Equal("casino.limit.tables");
        resolved.Meta.Should().BeEmpty();
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
