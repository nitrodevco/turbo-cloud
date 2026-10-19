using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Rooms;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The room visits log, for staff with <c>admin.players.view</c>: who went into which room is
/// what a player's page says about them. Read only.
/// </summary>
internal sealed class RoomVisitEndpoints(IGrainFactory grainFactory, AdminRoomVisits visits)
{
    private const string NO_ACCESS = "You can't look up players.";

    public void Map(RouteGroupBuilder secured) =>
        secured.MapGet("/room-visits", SearchAsync).AddEndpointFilter(RequireViewAsync);

    private async Task<IResult> SearchAsync(
        string? player,
        int? room,
        int? before,
        int? after,
        CancellationToken ct
    ) =>
        Results.Ok(await visits.SearchAsync(player, room, before, after, ct).ConfigureAwait(false));

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
