using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players.Notifications;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots.Permissions;

namespace Turbo.Players.Grains.Permissions;

internal sealed partial class PlayerPermissionGrain
{
    private static readonly string[] RESTRICTION_NODES =
    [
        PermissionNodes.Chat.SPEAK,
        PermissionNodes.TRADE,
    ];

    /// <summary>Login/reconnect only; capability and subscription resends do not repeat notices.</summary>
    public async Task NotifyActiveRestrictionsAsync(CancellationToken ct)
    {
        // A returning session receives its current restriction, never a backlog of elapsed ones.
        EnsureResolved(notifyRestrictionRestorations: false);
        foreach (var node in RESTRICTION_NODES)
            if (!EnsureResolved(notifyRestrictionRestorations: false).Has(node))
                await SendRestrictionNoticeAsync(node, restored: false, ct);
    }

    private string[] RestoredRestrictions(
        ResolvedPermissionsSnapshot current,
        DateTime now,
        string? explicitlyChangedNode
    )
    {
        if (
            _state.RestrictionInputs is not { } previous
            || previous.Resolved.NextExpiresAt is not { } expiry
            || expiry > now
        )
            return [];

        // Resolve the old inputs at the new time: an explicit removal must not masquerade as expiry.
        var expiryOnly = PermissionResolver.Resolve(
            previous.Registry,
            previous.Groups.Groups,
            previous.Assignments,
            now
        );
        return
        [
            .. RESTRICTION_NODES.Where(node =>
                !previous.Resolved.Has(node)
                && expiryOnly.Has(node)
                && current.Has(node)
                && (
                    explicitlyChangedNode is null
                    || PermissionNodeFormat.Specificity(explicitlyChangedNode, node)
                        == PermissionNodeFormat.NO_MATCH
                )
            ),
        ];
    }

    private async Task NotifyRestoredRestrictionsAsync(string[] nodes)
    {
        foreach (var node in nodes)
            if (_state.Resolved?.Has(node) == true)
                if (
                    await SendRestrictionNoticeAsync(node, restored: true, CancellationToken.None)
                    != PlayerNoticeDelivery.Sent
                )
                    return;
    }

    private Task<PlayerNoticeDelivery> SendRestrictionNoticeAsync(
        string node,
        bool restored,
        CancellationToken ct
    )
    {
        var chat = node == PermissionNodes.Chat.SPEAK;
        var key = chat
            ? restored
                ? "player.restriction.chat.restored"
                : "player.restriction.chat.active"
            : restored
                ? "player.restriction.trade.restored"
                : "player.restriction.trade.active";
        var text = chat
            ? restored
                ? "Your chat restriction has expired. You can speak again."
                : "Your ability to speak is currently restricted."
            : restored
                ? "Your trading restriction has expired. You can trade again."
                : "Your ability to trade is currently restricted.";
        return _noticeService.SendAsync(PlayerId, key, text, [], ct);
    }
}
