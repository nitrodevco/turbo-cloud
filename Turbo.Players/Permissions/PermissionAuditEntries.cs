using System;
using System.Globalization;
using Turbo.Database.Entities.Permissions;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Players.Permissions;

/// <summary>Builds <c>permission_audit</c> rows, so both permission grains write them one way.</summary>
internal static class PermissionAuditEntries
{
    public static PermissionAuditEntity Create(
        PermissionAuditTargetType targetType,
        int targetId,
        PermissionAuditActionType action,
        string subject,
        PlayerId? actor,
        string? value = null,
        DateTime? expiresAt = null
    ) =>
        new()
        {
            ActorPlayerId = actor?.Value,
            TargetType = targetType,
            TargetId = targetId,
            Action = action,
            Subject = subject,
            Value = value,
            ExpiresAt = expiresAt,
        };

    public static string Format(bool value) => value ? "true" : "false";

    public static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);
}
