using System.Collections.Immutable;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A player in at least one group besides default, with those groups.</summary>
public sealed record PermissionStaffMember(
    int Id,
    string Name,
    ImmutableArray<PlayerGroupView> Groups
);
