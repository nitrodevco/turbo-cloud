using System.Collections.Immutable;

namespace Turbo.Admin.Api.Contracts;

/// <summary>One player's permissions: what is set on them, the groups they reach, and what that resolves to.</summary>
public sealed record PlayerPermissionsResponse(
    int Id,
    string Name,
    ImmutableArray<PlayerGroupView> Groups,
    ImmutableArray<PermissionGroupRef> ReachedGroups,
    ImmutableArray<PermissionNodeView> Nodes,
    ImmutableArray<PermissionMetaView> Meta,
    ImmutableArray<string> Holds,
    ImmutableDictionary<string, string> ResolvedMeta,
    ImmutableArray<string> Unregistered,
    PermissionClientView Client,
    PermissionLevelView Level,
    bool CanEdit
);
