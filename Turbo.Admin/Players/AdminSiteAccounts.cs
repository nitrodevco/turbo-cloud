using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Players;

/// <summary>
/// The public site's side of a player's account, as staff change it: the Discord account linked to
/// them, and their sign-ins to the site. Rows of the site's own tables; no grain holds them.
/// </summary>
public sealed class AdminSiteAccounts(IDbContextFactory<TurboDbContext> dbCtxFactory)
{
    /// <summary>Unlinks the player's Discord account and ends their site sign-ins; false when none was linked.</summary>
    public async Task<bool> UnlinkDiscordAsync(PlayerId player, CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var unlinked = await db
            .PlayerDiscordLinks.Where(x => x.PlayerEntityId == player.Value)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        await db
            .WebSessions.Where(x => x.PlayerEntityId == player.Value)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        return unlinked > 0;
    }

    /// <summary>Ends every sign-in the player has on the public site; how many there were.</summary>
    public async Task<int> EndSessionsAsync(PlayerId player, CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        return await db
            .WebSessions.Where(x => x.PlayerEntityId == player.Value)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);
    }
}
