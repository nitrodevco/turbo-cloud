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

        return new PermissionEditor(
            grainFactory,
            registryProvider.Current,
            groups,
            resolved,
            player,
            timeProvider.GetUtcNow().UtcDateTime
        );
    }

    public async Task<PermissionEditChange> AddToGroupAsync(
        PlayerId? editor,
        PlayerId target,
        string group,
        TimeSpan? duration,
        PermissionExpiryModeType mode,
        CancellationToken ct
    )
    {
        DateTime? expiresAt = duration is { } span
            ? timeProvider.GetUtcNow().UtcDateTime + span
            : null;

        if (
            await CheckAsync(
                    editor,
                    target,
                    group,
                    null,
                    PermissionChange.MembershipAdded(target, group, expiresAt),
                    ct
                )
                .ConfigureAwait(false) is
            { } refused
        )
            return refused;

        var result = await grainFactory
            .GetPlayerPermissionGrain(target)
            .AddGroupAsync(group, expiresAt, mode, editor, ct)
            .ConfigureAwait(false);

        return new PermissionEditChange(
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

    public async Task<PermissionEditChange> RemoveFromGroupAsync(
        PlayerId? editor,
        PlayerId target,
        string group,
        bool? temporary,
        CancellationToken ct
    )
    {
        if (
            await CheckAsync(
                    editor,
                    target,
                    group,
                    null,
                    PermissionChange.MembershipRemoved(target, group, temporary),
                    ct
                )
                .ConfigureAwait(false) is
            { } refused
        )
            return refused;

        var grain = grainFactory.GetPlayerPermissionGrain(target);
        var result = await grain
            .RemoveGroupAsync(group, temporary ?? false, editor, ct)
            .ConfigureAwait(false);

        // Not said which: the permanent membership first, then the temporary one.
        if (temporary is null && result == PermissionChangeResultType.NotFound)
            result = await grain.RemoveGroupAsync(group, true, editor, ct).ConfigureAwait(false);

        return new PermissionEditChange(
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

    public async Task<PermissionEditChange> SetNodeAsync(
        PlayerId? editor,
        PlayerId target,
        string node,
        bool value,
        TimeSpan? duration,
        PermissionExpiryModeType mode,
        CancellationToken ct
    )
    {
        DateTime? expiresAt = duration is { } span
            ? timeProvider.GetUtcNow().UtcDateTime + span
            : null;

        if (
            await CheckAsync(
                    editor,
                    target,
                    null,
                    node,
                    PermissionChange.PlayerNodeSet(target, node, value, expiresAt),
                    ct
                )
                .ConfigureAwait(false) is
            { } refused
        )
            return refused;

        return new PermissionEditChange(
            PermissionEditRefusal.None,
            await grainFactory
                .GetPlayerPermissionGrain(target)
                .SetNodeAsync(node, value, expiresAt, mode, editor, ct)
                .ConfigureAwait(false),
            null
        );
    }

    public async Task<PermissionEditChange> UnsetNodeAsync(
        PlayerId? editor,
        PlayerId target,
        string node,
        bool temporary,
        CancellationToken ct
    )
    {
        if (
            await CheckAsync(
                    editor,
                    target,
                    null,
                    node,
                    PermissionChange.PlayerNodeUnset(target, node, temporary),
                    ct
                )
                .ConfigureAwait(false) is
            { } refused
        )
            return refused;

        return new PermissionEditChange(
            PermissionEditRefusal.None,
            await grainFactory
                .GetPlayerPermissionGrain(target)
                .UnsetNodeAsync(node, temporary, editor, ct)
                .ConfigureAwait(false),
            null
        );
    }

    /// <summary>
    /// The player, the group and the node are all the editor's to change, and the change leaves
    /// an editor who is a superuser one; null when all of it holds.
    /// </summary>
    private async Task<PermissionEditChange?> CheckAsync(
        PlayerId? editor,
        PlayerId target,
        string? group,
        string? node,
        PermissionChange change,
        CancellationToken ct
    )
    {
        var rule = await EditorForAsync(editor, ct).ConfigureAwait(false);

        if (
            group is not null
            && rule.CheckGroup(group) is not PermissionEditRefusal.None and var heavy
        )
            return PermissionEditChange.Refused(heavy);

        if (
            node is not null
            && rule.CheckAssignment(node) is not PermissionEditRefusal.None and var notHeld
        )
            return PermissionEditChange.Refused(notHeld);

        if (
            await rule.CheckPlayerAsync(target, ct).ConfigureAwait(false)
            is not PermissionEditRefusal.None
                and var refusal
        )
            return PermissionEditChange.Refused(refusal);

        return
            await rule.CheckKeepsAccessAsync(change, ct).ConfigureAwait(false)
                is not PermissionEditRefusal.None
                    and var keeps
            ? PermissionEditChange.Refused(keeps)
            : null;
    }
}
