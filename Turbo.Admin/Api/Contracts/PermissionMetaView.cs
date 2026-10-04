using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>A meta value set on a group or a player.</summary>
public sealed record PermissionMetaView(
    string Key,
    string Value,
    DateTime? ExpiresAtUtc,
    string? Description
);
