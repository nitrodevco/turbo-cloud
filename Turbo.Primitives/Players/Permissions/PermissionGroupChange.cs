using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>
/// What came of putting a player in a group or taking them out: refused by the edit rule, or the
/// grain's answer, and when it changed something, what to tell the player.
/// </summary>
public sealed record PermissionGroupChange(
    PermissionEditRefusal Refusal,
    PermissionChangeResultType Result,
    PermissionGroupNotice? Notice
)
{
    public static PermissionGroupChange Refused(PermissionEditRefusal refusal) =>
        new(refusal, PermissionChangeResultType.Unchanged, null);

    public bool IsRefused => Refusal != PermissionEditRefusal.None;
}
