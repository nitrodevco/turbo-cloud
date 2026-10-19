using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Players;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The currency log, every change to a wallet, for staff with <c>admin.players.view</c>, who see
/// players' wallets already. Read only.
/// </summary>
internal sealed class CurrencyLogEndpoints(IGrainFactory grainFactory, AdminCurrencyLog log)
{
    private const string NO_ACCESS = "You can't see players' wallets.";

    public void Map(RouteGroupBuilder secured) =>
        secured
            .MapGroup("/currency-log")
            .AddEndpointFilter(RequireViewAsync)
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

    private async ValueTask<object?> RequireViewAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var http = context.HttpContext;

        return await grainFactory
            .HasPermissionAsync(
                AdminIdentity.Of(http).PlayerId,
                PermissionNodes.Admin.PLAYERS_VIEW,
                http.RequestAborted
            )
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }
}
