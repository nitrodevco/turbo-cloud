using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Links;

/// <summary>
/// Who may be given a setup link, by whom. A setup link makes the passkey that signs in as the
/// player, so whoever holds one holds the account:
/// <list type="bullet">
/// <item>the server console may make one for anybody: it owns the machine already;</item>
/// <item>a player may make their own only while they have no passkey, so getting in the first
/// time works from the hotel but nobody can replace a passkey by being logged in to the game;</item>
/// <item>a link for somebody else needs <c>admin.passkeys.reset</c>, and only for a player whose
/// every node the issuer holds too: nobody can take over an account that can do more than
/// they can. This is the rule <c>docs/permissions.md</c> asks of anything that hands out
/// another player's powers.</item>
/// </list>
/// </summary>
public sealed class AdminLinkPolicy(IGrainFactory grainFactory)
{
    /// <param name="issuer">Who asks; null for the server console.</param>
    public async Task<AdminLinkDecision> CheckAsync(
        PlayerId? issuer,
        PlayerId target,
        CancellationToken ct
    )
    {
        var account = await grainFactory
            .GetAdminAccountGrain(target)
            .GetAccountAsync(ct)
            .ConfigureAwait(false);
        var replaces = account.Passkeys.Length > 0;

        if (issuer is not { } asker)
            return new(AdminLinkRefusal.None, replaces);

        if (asker == target)
            return new(replaces ? AdminLinkRefusal.AlreadySetUp : AdminLinkRefusal.None, false);

        if (
            !await grainFactory
                .HasPermissionAsync(asker, PermissionNodes.Admin.PASSKEYS_RESET, ct)
                .ConfigureAwait(false)
        )
            return new(AdminLinkRefusal.NeedsResetNode, replaces);

        var askerNodes = (
            await grainFactory
                .GetPlayerPermissionGrain(asker)
                .GetResolvedAsync(ct)
                .ConfigureAwait(false)
        ).Granted;
        var targetNodes = (
            await grainFactory
                .GetPlayerPermissionGrain(target)
                .GetResolvedAsync(ct)
                .ConfigureAwait(false)
        ).Granted;

        return new(
            targetNodes.IsSubsetOf(askerNodes)
                ? AdminLinkRefusal.None
                : AdminLinkRefusal.OutranksIssuer,
            replaces
        );
    }
}
