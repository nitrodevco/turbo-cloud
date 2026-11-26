using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Players.Snapshots.Permissions;

/// <summary>Everything set on one player: the input to resolution, beside the directory's groups.</summary>
[GenerateSerializer, Immutable]
public sealed record PlayerPermissionAssignmentsSnapshot
{
    public static readonly PlayerPermissionAssignmentsSnapshot EMPTY = new()
    {
        Groups = [],
        Nodes = [],
        Meta = [],
    };

    /// <summary>
    /// A player who holds just the group <paramref name="groupId"/>, besides default, and nothing
    /// of their own: what the group's members get, for its client level and for a preview of it.
    /// </summary>
    public static PlayerPermissionAssignmentsSnapshot MemberOf(int groupId) =>
        EMPTY with
        {
            Groups = [new PermissionGroupMembershipSnapshot { GroupId = groupId }],
        };

    /// <summary>Direct memberships. The default group is held whether it is listed or not.</summary>
    [Id(0)]
    public required ImmutableArray<PermissionGroupMembershipSnapshot> Groups { get; init; }

    [Id(1)]
    public required ImmutableArray<PermissionNodeAssignmentSnapshot> Nodes { get; init; }

    [Id(2)]
    public required ImmutableArray<PermissionMetaAssignmentSnapshot> Meta { get; init; }
}
