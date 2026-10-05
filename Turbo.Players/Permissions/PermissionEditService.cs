using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Providers;

namespace Turbo.Players.Permissions;

/// <summary>
/// <see cref="IPermissionEditService"/>: the edit rule, read from the editor's resolved
/// permissions and the directory's groups, then the change through the player's permission grain,
/// which saves and audits it, and the notice to send the player when it changed something.
/// </summary>
public sealed class PermissionEditService(
    IGrainFactory grainFactory,
    IPermissionRegistryProvider registryProvider,
    TimeProvider timeProvider
) : IPermissionEditService
{
    public const string ADDED_NOTICE = "command.group.add.notice";
    public const string REMOVED_NOTICE = "command.group.remove.notice";

    public async Task<PermissionEditor> EditorForAsync(PlayerId? editor, CancellationToken ct)
    {
        var groups = await grainFactory
            .GetPermissionGroupDirectoryGrain()
            .GetSnapshotAsync(ct)
            .ConfigureAwait(false);

        if (editor is not { } player)
            return PermissionEditor.ForConsole(grainFactory, registryProvider.Current, groups);

        var resolved = await grainFactory
            .GetPlayerPermissionGrain(player)
            .GetResolvedAsync(ct)
            .ConfigureAwait(false);

        return new PermissionEditor(grainFactory, registryProvider.Current, groups, resolved);
    }

    public async Task<PermissionGroupChange> AddToGroupAsync(
        PlayerId? editor,
        PlayerId target,
        string group,
        TimeSpan? duration,
        PermissionExpiryModeType mode,
        CancellationToken ct
    )
    {
        if (await CheckAsync(editor, target, group, ct).ConfigureAwait(false) is { } refused)
            return refused;

        var result = await grainFactory
            .GetPlayerPermissionGrain(target)
            .AddGroupAsync(
                group,
                duration is { } span ? timeProvider.GetUtcNow().UtcDateTime + span : null,
                mode,
                editor,
                ct
            )
            .ConfigureAwait(false);

        return new PermissionGroupChange(
            PermissionEditRefusal.None,
            result,
            result == PermissionChangeResultType.Changed
                ? new PermissionGroupNotice(
                    ADDED_NOTICE,
                    "You have been assigned to the group %0% (%1%).",
                    [group, new CommandDuration(duration).ToString()]
                )
                : null
        );
    }

    public async Task<PermissionGroupChange> RemoveFromGroupAsync(
        PlayerId? editor,
        PlayerId target,
        string group,
        bool? temporary,
        CancellationToken ct
    )
    {
        if (await CheckAsync(editor, target, group, ct).ConfigureAwait(false) is { } refused)
            return refused;

        var grain = grainFactory.GetPlayerPermissionGrain(target);
        var result = await grain
            .RemoveGroupAsync(group, temporary ?? false, editor, ct)
            .ConfigureAwait(false);

        // Not said which: the permanent membership first, then the temporary one.
        if (temporary is null && result == PermissionChangeResultType.NotFound)
            result = await grain.RemoveGroupAsync(group, true, editor, ct).ConfigureAwait(false);

        return new PermissionGroupChange(
            PermissionEditRefusal.None,
            result,
            result == PermissionChangeResultType.Changed
                ? new PermissionGroupNotice(
                    REMOVED_NOTICE,
                    "A %0% group assignment has been removed from your account.",
                    [group]
                )
                : null
        );
    }

    /// <summary>The player and the group are both the editor's to change; null when they are.</summary>
    private async Task<PermissionGroupChange?> CheckAsync(
        PlayerId? editor,
        PlayerId target,
        string group,
        CancellationToken ct
    )
    {
        var rule = await EditorForAsync(editor, ct).ConfigureAwait(false);

        if (rule.CheckGroup(group) is not PermissionEditRefusal.None and var heavy)
            return PermissionGroupChange.Refused(heavy);

        return
            await rule.CheckPlayerAsync(target, ct).ConfigureAwait(false)
                is not PermissionEditRefusal.None
                    and var refusal
            ? PermissionGroupChange.Refused(refusal)
            : null;
    }
}
