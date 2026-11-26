using System.Collections.Immutable;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// Why a player does or does not hold a node: what decided, and what it beat. While they preview a
/// group (<see cref="Preview"/>), the answer is that group's.
/// </summary>
public sealed record PermissionCheckResponse(
    string Node,
    bool IsRegistered,
    bool Granted,
    PermissionSourceView? Decision,
    ImmutableArray<PermissionSourceView> Overridden,
    PermissionPreviewView? Preview
);
