using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A group or player given a node, exactly or by wildcard.</summary>
public sealed record PermissionHolderView(
    string TargetType,
    int TargetId,
    string TargetName,
    string Node,
    bool Value,
    DateTime? ExpiresAtUtc
);
