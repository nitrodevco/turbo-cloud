using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Notifications;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The panel's bell, for anyone signed in to the panel: bans only for those who may look up
/// players, refused commands only for those who may read the command log.
/// </summary>
internal sealed class NotificationEndpoints(
    IGrainFactory grainFactory,
    AdminNotificationQueries notifications
)
{
    public void Map(RouteGroupBuilder secured) => secured.MapGet("/notifications", GetAsync);

    private async Task<IResult> GetAsync(HttpContext http, CancellationToken ct)
    {
        var resolved = await grainFactory
            .GetPlayerPermissionGrain(AdminIdentity.Of(http).PlayerId)
            .GetResolvedAsync(ct)
            .ConfigureAwait(false);
        var scope = new NotificationScope(
            resolved.Has(PermissionNodes.Admin.PLAYERS_VIEW),
            resolved.Has(PermissionNodes.Admin.COMMAND_LOG_VIEW)
        );

        return Results.Ok(await notifications.GetAsync(scope, ct).ConfigureAwait(false));
    }
}
