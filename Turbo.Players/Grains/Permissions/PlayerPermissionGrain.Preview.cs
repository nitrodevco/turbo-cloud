using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Players.Grains.Permissions;

/// <summary>
/// Previewing a group: for a while the player holds what a plain member of it holds, so staff can
/// see the hotel and the panel as that group does (<c>docs/permissions.md</c>, §9). Nothing is
/// written: the preview lives in this activation, and the player's own rows go on resolving
/// beside it into <see cref="PlayerPermissionLiveState.Standing"/>.
/// </summary>
internal sealed partial class PlayerPermissionGrain
{
    public async Task<PermissionPreviewResultSnapshot> StartPreviewAsync(
        string groupName,
        CancellationToken ct
    )
    {
        EnsureResolved();

        var now = UtcNow;

        if (FindGroup(groupName) is not { } group)
            return new() { Result = PermissionPreviewResultType.UnknownGroup };

        var previewed = PermissionPreview.MemberOf(_state.Registry!, _state.Groups, group, now);

        if (
            PermissionPreview.Refusal(_state.Groups, _state.Standing!, group, previewed) is
            { } refusal
        )
        {
            _logger.LogWarning(
                "Player {PlayerId} may not preview permission group {Group}: {Result} {Node}",
                PlayerId,
                group.Name,
                refusal.Result,
                refusal.Node
            );

            return refusal;
        }

        var duration = TimeSpan.FromMilliseconds(_permissionConfig.PreviewDurationMs);

        _state.Preview = new PermissionPreviewSnapshot
        {
            GroupName = group.Name,
            DisplayName = group.DisplayName,
            EndsAt = now + duration,
        };

        var resolved = Resolve();

        _logger.LogInformation(
            "Player {PlayerId} is previewing permission group {Group} until {EndsAt}",
            PlayerId,
            group.Name,
            _state.Preview.EndsAt
        );

        await PublishChangesAsync(force: false, ct);

        _noticeService
            .SendAsync(
                PlayerId,
                "player.permissions.preview.started",
                "You're seeing the hotel as %0% now. Stop it from the banner in the admin panel, or it ends by itself in %1% minutes.",
                [
                    group.DisplayName,
                    ((int)duration.TotalMinutes).ToString(CultureInfo.InvariantCulture),
                ],
                CancellationToken.None
            )
            .LogAndForget(_logger, "tell player {PlayerId} their preview started", PlayerId);

        return new() { Result = PermissionPreviewResultType.Started, Preview = resolved.Preview };
    }

    public async Task<bool> EndPreviewAsync(CancellationToken ct)
    {
        if (_state.Preview is not { } preview)
            return false;

        EndPreview(preview, "stopped");
        Resolve();

        await PublishChangesAsync(force: false, ct);

        return true;
    }

    /// <summary>
    /// What the player holds while a preview runs: a plain member's of the group, worked out
    /// against the same registry and groups as <paramref name="standing"/>. <c>null</c> when no
    /// preview runs. A preview that ran out, whose group is gone, or that the rule would no longer
    /// allow (the group made heavier, a node it gives taken from the player, a sanction on them)
    /// ends here, so the player is never held to more than they hold themselves.
    /// </summary>
    private ResolvedPermissionsSnapshot? ResolvePreview(
        PermissionRegistry registry,
        ResolvedPermissionsSnapshot standing,
        DateTime now
    )
    {
        if (_state.Preview is not { } preview)
            return null;

        if (preview.EndsAt <= now)
        {
            EndPreview(preview, "ran out");

            return null;
        }

        if (FindGroup(preview.GroupName) is not { } group)
        {
            EndPreview(preview, "its group was deleted");

            return null;
        }

        var previewed = PermissionPreview.MemberOf(registry, _state.Groups, group, now);

        if (PermissionPreview.Refusal(_state.Groups, standing, group, previewed) is { } refusal)
        {
            EndPreview(preview, $"{refusal.Result} {refusal.Node}".TrimEnd());

            return null;
        }

        _state.Preview = preview with { DisplayName = group.DisplayName };

        return previewed with
        {
            VerboseFilter = _state.VerboseFilter,
            Preview = _state.Preview,
            // Worked out again when the preview ends, and when the player's own rows change
            // (a sanction running out may end it too).
            NextExpiresAt = Earliest(
                Earliest(previewed.NextExpiresAt, standing.NextExpiresAt),
                preview.EndsAt
            ),
        };
    }

    /// <summary>Forgets the preview and tells the player they are themselves again; resolving is the caller's.</summary>
    private void EndPreview(PermissionPreviewSnapshot preview, string why)
    {
        _state.Preview = null;

        _logger.LogInformation(
            "Player {PlayerId} stopped previewing permission group {Group}: {Why}",
            PlayerId,
            preview.GroupName,
            why
        );

        _noticeService
            .SendAsync(
                PlayerId,
                "player.permissions.preview.ended",
                "You're no longer seeing the hotel as %0%: your own permissions are back.",
                [preview.DisplayName],
                CancellationToken.None
            )
            .LogAndForget(_logger, "tell player {PlayerId} their preview ended", PlayerId);
    }
}
