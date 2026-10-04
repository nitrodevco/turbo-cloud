using System;
using System.Collections.Immutable;

namespace Turbo.Admin.Api.Contracts;

/// <summary>An assignment weighed for a node: on the player, or on a group reached by the path given.</summary>
public sealed record PermissionSourceView(
    string Source,
    string? GroupName,
    int? GroupWeight,
    ImmutableArray<string> Path,
    string Node,
    bool Value,
    DateTime? ExpiresAtUtc
);
