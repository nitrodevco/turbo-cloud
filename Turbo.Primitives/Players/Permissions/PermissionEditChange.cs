using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>
/// What came of changing a player's groups or nodes: refused by the edit rule, or the grain's
/// answer, and when a group change changed something, what to tell the player.
/// </summary>
public sealed record PermissionEditChange(
    PermissionEditRefusal Refusal,
    PermissionChangeResultType Result,
    PermissionGroupNotice? Notice
)
{
    public static PermissionEditChange Refused(PermissionEditRefusal refusal) =>
        new(refusal, PermissionChangeResultType.Unchanged, null);

    public bool IsRefused => Refusal != PermissionEditRefusal.None;
}
