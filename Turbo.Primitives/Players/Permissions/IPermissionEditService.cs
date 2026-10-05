using System;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Primitives.Players.Permissions;

/// <summary>
/// Changing permissions on someone's behalf, under <see cref="PermissionEditor"/>'s rule: the one
/// place the admin panel and <c>:group</c> both go through, so neither can hand out more than the
/// other. <c>editor</c> is who asks, and <c>null</c> for the server console, which may do anything.
/// </summary>
public interface IPermissionEditService
{
    /// <summary>What <paramref name="editor"/> may change, as of the groups and their permissions now.</summary>
    public Task<PermissionEditor> EditorForAsync(PlayerId? editor, CancellationToken ct);

    /// <summary>
    /// Puts <paramref name="target"/> in <paramref name="group"/>, for <paramref name="duration"/>
    /// or for good, when the editor may hand out that group to that player.
    /// </summary>
    public Task<PermissionGroupChange> AddToGroupAsync(
        PlayerId? editor,
        PlayerId target,
        string group,
        TimeSpan? duration,
        PermissionExpiryModeType mode,
        CancellationToken ct
    );

    /// <summary>
    /// Takes <paramref name="target"/> out of <paramref name="group"/>: the temporary membership,
    /// the permanent one, or, with <paramref name="temporary"/> null, whichever there is, the
    /// permanent one first.
    /// </summary>
    public Task<PermissionGroupChange> RemoveFromGroupAsync(
        PlayerId? editor,
        PlayerId target,
        string group,
        bool? temporary,
        CancellationToken ct
    );
}
