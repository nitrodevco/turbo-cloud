using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A node or wildcard set on a group or a player; the description is the registered node's, for an exact one.</summary>
public sealed record PermissionNodeView(
    string Node,
    bool Value,
    DateTime? ExpiresAtUtc,
    string? Description
);
