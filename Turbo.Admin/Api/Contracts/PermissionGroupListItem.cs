using System.Collections.Immutable;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A group in the panel's list of groups.</summary>
public sealed record PermissionGroupListItem(
    int Id,
    string Name,
    string DisplayName,
    int Weight,
    ImmutableArray<string> Parents,
    int NodeCount,
    int MetaCount,
    bool CanEdit
);
