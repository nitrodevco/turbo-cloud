using System.Collections.Immutable;

namespace Turbo.Admin.Api.Contracts;

/// <summary>One group: its own nodes and meta, its parents and the groups that inherit it, and the level a member would get.</summary>
public sealed record PermissionGroupResponse(
    int Id,
    string Name,
    string DisplayName,
    int Weight,
    bool IsDefault,
    ImmutableArray<PermissionGroupRef> Parents,
    ImmutableArray<PermissionGroupRef> Children,
    ImmutableArray<PermissionNodeView> Nodes,
    ImmutableArray<PermissionMetaView> Meta,
    PermissionLevelView Level,
    bool CanEdit
);
