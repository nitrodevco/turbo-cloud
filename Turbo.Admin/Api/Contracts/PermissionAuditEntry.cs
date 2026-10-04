using System;

namespace Turbo.Admin.Api.Contracts;

/// <summary>One row of the permission log, with names for whom it is about and who made it (none for the console).</summary>
public sealed record PermissionAuditEntry(
    DateTime AtUtc,
    int? ActorId,
    string? ActorName,
    string TargetType,
    int TargetId,
    string TargetName,
    string Action,
    string Subject,
    string? Value,
    DateTime? ExpiresAtUtc
);
