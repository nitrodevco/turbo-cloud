using System.Collections.Immutable;

namespace Turbo.Admin.Api.Contracts;

/// <summary>Every group, heaviest first, and how far the signed-in staff member may edit them.</summary>
public sealed record PermissionGroupsResponse(
    ImmutableArray<PermissionGroupListItem> Groups,
    bool CanManage,
    int YourWeight
);
