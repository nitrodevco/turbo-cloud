using System;
using System.Collections.Immutable;
using System.Linq;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>
/// An edit of groups or of one player's groups, described well enough to work out what it does to
/// somebody's permissions before it is made: <see cref="PermissionEditor.CheckKeepsAccessAsync"/>
/// applies it to the editor's own groups and assignments and resolves them again, to refuse the
/// change that would take their own <c>permissions.superuser</c> away. Mirrors what the grains do
/// with the same call: a permanent and a temporary row of a node are separate rows, and a
/// removal says which it means.
/// </summary>
public abstract record PermissionChange
{
    /// <summary>The groups as they would be after the change.</summary>
    public virtual PermissionGroupDirectorySnapshot Apply(
        PermissionGroupDirectorySnapshot groups
    ) => groups;

    /// <summary>One player's own groups and nodes as they would be after the change.</summary>
    public virtual PlayerPermissionAssignmentsSnapshot Apply(
        PlayerId player,
        PermissionGroupDirectorySnapshot groups,
        PlayerPermissionAssignmentsSnapshot assignments
    ) => assignments;

    public static PermissionChange GroupDeleted(string group) => new GroupDeletedChange(group);

    public static PermissionChange GroupWeightSet(string group, int weight) =>
        new GroupWeightChange(group, weight);

    public static PermissionChange GroupNodeSet(
        string group,
        string node,
        bool value,
        DateTime? expiresAt
    ) => new GroupNodeSetChange(group, node, value, expiresAt);

    public static PermissionChange GroupNodeUnset(string group, string node, bool temporary) =>
        new GroupNodeUnsetChange(group, node, temporary);

    public static PermissionChange GroupParentAdded(string group, string parent) =>
        new GroupParentChange(group, parent, true);

    public static PermissionChange GroupParentRemoved(string group, string parent) =>
        new GroupParentChange(group, parent, false);

    public static PermissionChange MembershipAdded(
        PlayerId player,
        string group,
        DateTime? expiresAt
    ) => new MembershipAddedChange(player, group, expiresAt);

    /// <param name="temporary">Which membership goes; null for both, as <c>:group remove</c> does.</param>
    public static PermissionChange MembershipRemoved(
        PlayerId player,
        string group,
        bool? temporary
    ) => new MembershipRemovedChange(player, group, temporary);

    private static bool IsTemporary(DateTime? expiresAt) => expiresAt is not null;

    private static PermissionGroupSnapshot? Find(
        PermissionGroupDirectorySnapshot groups,
        string name
    ) => groups.Groups.Values.FirstOrDefault(x => x.Name == name);

    private static PermissionGroupDirectorySnapshot Replace(
        PermissionGroupDirectorySnapshot groups,
        PermissionGroupSnapshot group
    ) => groups with { Groups = groups.Groups.SetItem(group.Id, group) };

    private sealed record GroupDeletedChange(string Group) : PermissionChange
    {
        public override PermissionGroupDirectorySnapshot Apply(
            PermissionGroupDirectorySnapshot groups
        ) =>
            Find(groups, Group) is { } found
                ? groups with
                {
                    Groups = groups.Groups.Remove(found.Id),
                }
                : groups;
    }

    private sealed record GroupWeightChange(string Group, int Weight) : PermissionChange
    {
        public override PermissionGroupDirectorySnapshot Apply(
            PermissionGroupDirectorySnapshot groups
        ) =>
            Find(groups, Group) is { } found
                ? Replace(groups, found with { Weight = Weight })
                : groups;
    }

    private sealed record GroupNodeSetChange(
        string Group,
        string Node,
        bool Value,
        DateTime? ExpiresAt
    ) : PermissionChange
    {
        public override PermissionGroupDirectorySnapshot Apply(
            PermissionGroupDirectorySnapshot groups
        ) =>
            Find(groups, Group) is { } found
                ? Replace(
                    groups,
                    found with
                    {
                        Nodes =
                        [
                            .. found.Nodes.Where(x =>
                                x.Node != Node || IsTemporary(x.ExpiresAt) != IsTemporary(ExpiresAt)
                            ),
                            new PermissionNodeAssignmentSnapshot
                            {
                                Node = Node,
                                Value = Value,
                                ExpiresAt = ExpiresAt,
                            },
                        ],
                    }
                )
                : groups;
    }

    private sealed record GroupNodeUnsetChange(string Group, string Node, bool Temporary)
        : PermissionChange
    {
        public override PermissionGroupDirectorySnapshot Apply(
            PermissionGroupDirectorySnapshot groups
        ) =>
            Find(groups, Group) is { } found
                ? Replace(
                    groups,
                    found with
                    {
                        Nodes =
                        [
                            .. found.Nodes.Where(x =>
                                x.Node != Node || IsTemporary(x.ExpiresAt) != Temporary
                            ),
                        ],
                    }
                )
                : groups;
    }

    private sealed record GroupParentChange(string Group, string Parent, bool Added)
        : PermissionChange
    {
        public override PermissionGroupDirectorySnapshot Apply(
            PermissionGroupDirectorySnapshot groups
        )
        {
            if (Find(groups, Group) is not { } found || Find(groups, Parent) is not { } parent)
                return groups;

            var parents = found.ParentIds.Remove(parent.Id);

            return Replace(
                groups,
                found with
                {
                    ParentIds = Added ? parents.Add(parent.Id) : parents,
                }
            );
        }
    }

    private sealed record MembershipAddedChange(PlayerId Player, string Group, DateTime? ExpiresAt)
        : PermissionChange
    {
        public override PlayerPermissionAssignmentsSnapshot Apply(
            PlayerId player,
            PermissionGroupDirectorySnapshot groups,
            PlayerPermissionAssignmentsSnapshot assignments
        ) =>
            player != Player || Find(groups, Group) is not { } found
                ? assignments
                : assignments with
                {
                    Groups =
                    [
                        .. assignments.Groups.Where(x =>
                            x.GroupId != found.Id
                            || IsTemporary(x.ExpiresAt) != IsTemporary(ExpiresAt)
                        ),
                        new PermissionGroupMembershipSnapshot
                        {
                            GroupId = found.Id,
                            ExpiresAt = ExpiresAt,
                        },
                    ],
                };
    }

    private sealed record MembershipRemovedChange(PlayerId Player, string Group, bool? Temporary)
        : PermissionChange
    {
        public override PlayerPermissionAssignmentsSnapshot Apply(
            PlayerId player,
            PermissionGroupDirectorySnapshot groups,
            PlayerPermissionAssignmentsSnapshot assignments
        ) =>
            player != Player || Find(groups, Group) is not { } found
                ? assignments
                : assignments with
                {
                    Groups =
                    [
                        .. assignments.Groups.Where(x =>
                            x.GroupId != found.Id
                            || (Temporary is { } temporary && IsTemporary(x.ExpiresAt) != temporary)
                        ),
                    ],
                };
    }
}
