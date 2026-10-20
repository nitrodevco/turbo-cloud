using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Turbo.Admin.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The currency log, every change to a wallet, for staff with <c>admin.players.view</c>, who see
/// players' wallets already. Read only.
/// </summary>
internal sealed class CurrencyLogEndpoints(AdminCurrencyLog log)
{
    private const string NO_ACCESS = "You can't see players' wallets.";

    public void Map(RouteGroupBuilder secured) =>
        secured
            .MapGroup("/currency-log")
            .RequirePermission(PermissionNodes.Admin.PLAYERS_VIEW, NO_ACCESS)
            .MapGet("/", SearchAsync);

    private async Task<IResult> SearchAsync(
        string? player,
        int? currency,
        string? cause,
        long? before,
        long? after,
        CancellationToken ct
    ) =>
        Results.Ok(
            await log.SearchAsync(player, currency, cause, before, after, ct).ConfigureAwait(false)
        );
}
