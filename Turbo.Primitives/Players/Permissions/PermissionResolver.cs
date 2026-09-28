using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>
/// Works out what one player holds, as a pure function of their own assignments, the groups the
/// directory holds, the registry and the time. See <c>docs/permissions.md</c> §5.
/// <para>
/// The value of a node comes from the first source with an opinion on it: the player's own
/// assignments, then each group they hold, directly or by inheritance, highest weight first
/// (name breaks a tie, so the order is stable). Within a source the most specific assignment
/// wins — the node itself, then the longest wildcard — and at equal specificity a denial beats a
/// grant. No opinion anywhere: denied. Expired assignments and memberships do not take part.
/// Meta resolves in the same source order.
/// </para>
/// </summary>
public static class PermissionResolver
{
    public static ResolvedPermissionsSnapshot Resolve(
        PermissionRegistry registry,
        IReadOnlyDictionary<int, PermissionGroupSnapshot> groups,
        PlayerPermissionAssignmentsSnapshot player,
        DateTime now
    )
    {
        var sources = CollectSources(groups, player, now, out var nextExpiresAt);

        var granted = ImmutableHashSet.CreateBuilder<string>(StringComparer.Ordinal);

        foreach (var node in registry.Nodes.Keys)
        {
            foreach (var source in sources)
            {
                var match = BestMatch(source, node);

                if (match is null)
                    continue;

                if (match.Value)
                    granted.Add(node);

                break;
            }
        }

        var meta = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var unregisteredNodes = new SortedSet<string>(StringComparer.Ordinal);
        var unregisteredMetaKeys = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var source in sources)
        {
            foreach (var assignment in source.Meta)
            {
                if (!registry.IsRegisteredMetaKey(assignment.Key))
                    unregisteredMetaKeys.Add(assignment.Key);
                else
                    meta.TryAdd(assignment.Key, assignment.Value);
            }

            foreach (var assignment in source.Nodes)
            {
                if (
                    !PermissionNodeFormat.IsWildcard(assignment.Node)
                    && !registry.IsRegistered(assignment.Node)
                )
                    unregisteredNodes.Add(assignment.Node);
            }
        }

        return new ResolvedPermissionsSnapshot
        {
            Granted = granted.ToImmutable(),
            Meta = meta.ToImmutable(),
            NextExpiresAt = nextExpiresAt,
            UnregisteredNodes = [.. unregisteredNodes],
            UnregisteredMetaKeys = [.. unregisteredMetaKeys],
        };
    }

    /// <summary>
    /// Why <paramref name="node"/> resolves the way it does for this player: the deciding
    /// assignment, and the best match of every lower-priority source it beat.
    /// </summary>
    public static PermissionCheckSnapshot Explain(
        PermissionRegistry registry,
        IReadOnlyDictionary<int, PermissionGroupSnapshot> groups,
        PlayerPermissionAssignmentsSnapshot player,
        string node,
        DateTime now
    )
    {
        var sources = CollectSources(groups, player, now, out _);

        PermissionAssignmentSourceSnapshot? decision = null;
        var overridden = ImmutableArray.CreateBuilder<PermissionAssignmentSourceSnapshot>();

        foreach (var source in sources)
        {
            var match = BestMatch(source, node);

            if (match is null)
                continue;

            var described = Describe(source, match);

            if (decision is null)
                decision = described;
            else
                overridden.Add(described);
        }

        return new PermissionCheckSnapshot
        {
            Node = node,
            IsRegistered = registry.IsRegistered(node),
            Granted = decision?.Value ?? false,
            Decision = decision,
            Overridden = overridden.ToImmutable(),
        };
    }

    /// <summary>
    /// The player's own source first, then every group reached from their unexpired memberships
    /// and the default group, each once, highest weight first. Also reports the earliest expiry
    /// among everything that took part.
    /// </summary>
    private static List<Source> CollectSources(
        IReadOnlyDictionary<int, PermissionGroupSnapshot> groups,
        PlayerPermissionAssignmentsSnapshot player,
        DateTime now,
        out DateTime? nextExpiresAt
    )
    {
        DateTime? earliest = null;

        void Track(DateTime? expiresAt)
        {
            if (expiresAt is { } at && (earliest is null || at < earliest))
                earliest = at;
        }

        bool IsLive(DateTime? expiresAt)
        {
            if (expiresAt is { } at && at <= now)
                return false;

            Track(expiresAt);

            return true;
        }

        var sources = new List<Source>
        {
            new(
                null,
                [],
                [.. player.Nodes.Where(x => IsLive(x.ExpiresAt))],
                [.. player.Meta.Where(x => IsLive(x.ExpiresAt))]
            ),
        };

        // Breadth first from every held group at once, so each group is reached along its
        // shortest path and counted once, however many paths lead to it; the visited set also
        // ends a cycle.
        var queue = new Queue<(int GroupId, ImmutableArray<string> Path)>();

        foreach (var membership in player.Groups)
        {
            if (IsLive(membership.ExpiresAt))
                queue.Enqueue((membership.GroupId, []));
        }

        foreach (var group in groups.Values)
        {
            if (group.Name == PermissionGroupNames.DEFAULT)
                queue.Enqueue((group.Id, []));
        }

        var visited = new HashSet<int>();
        var groupSources = new List<Source>();

        while (queue.Count > 0)
        {
            var (groupId, parentPath) = queue.Dequeue();

            // A membership or parent pointing at a deleted group simply leads nowhere.
            if (!groups.TryGetValue(groupId, out var group) || !visited.Add(groupId))
                continue;

            var path = parentPath.Add(group.Name);

            groupSources.Add(
                new(
                    group,
                    path,
                    [.. group.Nodes.Where(x => IsLive(x.ExpiresAt))],
                    [.. group.Meta.Where(x => IsLive(x.ExpiresAt))]
                )
            );

            foreach (var parentId in group.ParentIds)
                queue.Enqueue((parentId, path));
        }

        sources.AddRange(
            groupSources
                .OrderByDescending(x => x.Group!.Weight)
                .ThenBy(x => x.Group!.Name, StringComparer.Ordinal)
        );

        nextExpiresAt = earliest;

        return sources;
    }

    private static PermissionNodeAssignmentSnapshot? BestMatch(Source source, string node)
    {
        PermissionNodeAssignmentSnapshot? best = null;
        var bestSpecificity = PermissionNodeFormat.NO_MATCH;

        foreach (var assignment in source.Nodes)
        {
            var specificity = PermissionNodeFormat.Specificity(assignment.Node, node);

            if (specificity == PermissionNodeFormat.NO_MATCH)
                continue;

            if (
                specificity > bestSpecificity
                || (specificity == bestSpecificity && !assignment.Value && best!.Value)
            )
            {
                best = assignment;
                bestSpecificity = specificity;
            }
        }

        return best;
    }

    private static PermissionAssignmentSourceSnapshot Describe(
        Source source,
        PermissionNodeAssignmentSnapshot assignment
    ) =>
        new()
        {
            SourceType = source.Group is null
                ? PermissionSourceType.Player
                : PermissionSourceType.Group,
            GroupId = source.Group?.Id,
            GroupName = source.Group?.Name,
            GroupWeight = source.Group?.Weight,
            Path = source.Path,
            Node = assignment.Node,
            Value = assignment.Value,
            ExpiresAt = assignment.ExpiresAt,
        };

    /// <summary>One place assignments come from; <see cref="Group"/> is null for the player.</summary>
    private sealed record Source(
        PermissionGroupSnapshot? Group,
        ImmutableArray<string> Path,
        ImmutableArray<PermissionNodeAssignmentSnapshot> Nodes,
        ImmutableArray<PermissionMetaAssignmentSnapshot> Meta
    );
}
