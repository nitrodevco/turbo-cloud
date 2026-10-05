using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Players;

/// <summary>
/// Who may handle a player's account from the panel, and how. A login ticket logs in as the player,
/// so it is the account; unlinking their Discord or ending their site sign-ins locks them out.
/// Either needs its node (<c>admin.tickets.issue</c>, <c>admin.accounts.manage</c>) and, for anyone
/// but the staff member themselves, every node the player holds, the rule <c>docs/permissions.md</c>
/// asks of anything acting with another player's powers. Otherwise a staff member could log in as,
/// or lock out, someone who can do more.
/// </summary>
public sealed class AdminTicketPolicy(IGrainFactory grainFactory)
{
    /// <summary>Why the issuer may not give the player a ticket, in words; null when they may.</summary>
    public Task<string?> RefusalAsync(PlayerId issuer, PlayerId player, CancellationToken ct) =>
        RefusalAsync(
            issuer,
            player,
            PermissionNodes.Admin.TICKETS_ISSUE,
            "You can't issue login tickets.",
            ct
        );

    /// <summary>Why the staff member may not do what <paramref name="node"/> allows to the player; null when they may.</summary>
    public async Task<string?> RefusalAsync(
        PlayerId issuer,
        PlayerId player,
        string node,
        string withoutNode,
        CancellationToken ct
    )
    {
        var issuerNodes = (
            await grainFactory
                .GetPlayerPermissionGrain(issuer)
                .GetResolvedAsync(ct)
                .ConfigureAwait(false)
        ).Granted;

        if (!issuerNodes.Contains(node))
            return withoutNode;

        if (issuer == player)
            return null;

        var playerNodes = (
            await grainFactory
                .GetPlayerPermissionGrain(player)
                .GetResolvedAsync(ct)
                .ConfigureAwait(false)
        ).Granted;

        return playerNodes.IsSubsetOf(issuerNodes)
            ? null
            : "This player can do things you can't, so you can't do this to their account.";
    }
}
