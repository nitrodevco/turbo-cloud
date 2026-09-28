using System.Collections.Generic;
using System.Linq;
using Turbo.Database.Entities.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Database.Extensions;

/// <summary>
/// Row to snapshot for permissions, shared by the group directory (every group) and the player
/// permission grain (one player's own assignments).
/// </summary>
public static class PermissionEntityExtensions
{
    /// <summary>A group with its parents, nodes and meta loaded (<c>Include</c>d).</summary>
    public static PermissionGroupSnapshot ToSnapshot(this PermissionGroupEntity entity) =>
        new()
        {
            Id = entity.Id,
            Name = entity.Name,
            DisplayName = entity.DisplayName,
            Weight = entity.Weight,
            ParentIds = [.. (entity.Parents ?? []).Select(x => x.ParentGroupEntityId)],
            Nodes = [.. (entity.Nodes ?? []).Select(x => x.ToSnapshot())],
            Meta = [.. (entity.Meta ?? []).Select(x => x.ToSnapshot())],
        };

    public static PermissionNodeAssignmentSnapshot ToSnapshot(
        this PermissionGroupNodeEntity entity
    ) =>
        new()
        {
            Node = entity.Node,
            Value = entity.Value,
            ExpiresAt = entity.ExpiresAt,
        };

    public static PermissionMetaAssignmentSnapshot ToSnapshot(
        this PermissionGroupMetaEntity entity
    ) =>
        new()
        {
            Key = entity.Key,
            Value = entity.Value,
            ExpiresAt = entity.ExpiresAt,
        };

    public static PermissionNodeAssignmentSnapshot ToSnapshot(
        this PlayerPermissionNodeEntity entity
    ) =>
        new()
        {
            Node = entity.Node,
            Value = entity.Value,
            ExpiresAt = entity.ExpiresAt,
        };

    public static PermissionMetaAssignmentSnapshot ToSnapshot(
        this PlayerPermissionMetaEntity entity
    ) =>
        new()
        {
            Key = entity.Key,
            Value = entity.Value,
            ExpiresAt = entity.ExpiresAt,
        };

    public static PermissionGroupMembershipSnapshot ToSnapshot(
        this PlayerPermissionGroupEntity entity
    ) => new() { GroupId = entity.GroupEntityId, ExpiresAt = entity.ExpiresAt };

    /// <summary>One player's rows, read separately, as the resolver's input.</summary>
    public static PlayerPermissionAssignmentsSnapshot ToAssignmentsSnapshot(
        IEnumerable<PlayerPermissionGroupEntity> groups,
        IEnumerable<PlayerPermissionNodeEntity> nodes,
        IEnumerable<PlayerPermissionMetaEntity> meta
    ) =>
        new()
        {
            Groups = [.. groups.Select(x => x.ToSnapshot())],
            Nodes = [.. nodes.Select(x => x.ToSnapshot())],
            Meta = [.. meta.Select(x => x.ToSnapshot())],
        };
}
