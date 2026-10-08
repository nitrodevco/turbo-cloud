using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Search;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The panel's search (Ctrl K), for anyone signed in to the panel: each kind of hit only for those
/// who may open its page, so the search shows nobody more than the pages would.
/// </summary>
internal sealed class SearchEndpoints(IGrainFactory grainFactory, AdminSearchQueries search)
{
    public void Map(RouteGroupBuilder secured) => secured.MapGet("/search", SearchAsync);

    private async Task<IResult> SearchAsync(string? q, HttpContext http, CancellationToken ct)
    {
        var resolved = await grainFactory
            .GetPlayerPermissionGrain(AdminIdentity.Of(http).PlayerId)
            .GetResolvedAsync(ct)
            .ConfigureAwait(false);
        var scope = new SearchScope(
            resolved.Has(PermissionNodes.Admin.PLAYERS_VIEW),
            resolved.Has(PermissionNodes.Admin.ROOMS_VIEW),
            resolved.Has(PermissionNodes.Admin.CATALOG_VIEW),
            resolved.Has(PermissionNodes.Admin.GAMEDATA_VIEW)
        );

        return Results.Ok(await search.SearchAsync(q, scope, ct).ConfigureAwait(false));
    }
}
