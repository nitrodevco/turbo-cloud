using System;
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
/// Finding and inspecting rooms, for staff with <c>admin.rooms.view</c>. Read-only: nothing here
/// changes a room or loads one that is not loaded.
/// </summary>
internal sealed class RoomEndpoints(IGrainFactory grainFactory, AdminRoomQueries rooms)
{
    private const string NO_ACCESS = "You can't look up rooms.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured.MapGroup("/rooms").AddEndpointFilter(RequireRoomsViewAsync);

        group.MapGet("/", SearchAsync);
        group.MapGet("/{id:int}", GetAsync);
    }

    private async Task<IResult> SearchAsync(
        string? q,
        string? by,
        int? page,
        CancellationToken ct
    ) => Results.Ok(await rooms.SearchAsync(q, ModeOf(by), page ?? 1, ct).ConfigureAwait(false));

    private async Task<IResult> GetAsync(int id, CancellationToken ct) =>
        await rooms.GetAsync(id, ct).ConfigureAwait(false) is { } room
            ? Results.Ok(room)
            : AdminResults.Error(StatusCodes.Status404NotFound, $"There is no room {id}.");

    private static RoomSearchMode ModeOf(string? by) =>
        Enum.TryParse<RoomSearchMode>(by, ignoreCase: true, out var mode)
            ? mode
            : RoomSearchMode.Name;

    private async ValueTask<object?> RequireRoomsViewAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var http = context.HttpContext;

        return await grainFactory
            .HasPermissionAsync(
                AdminIdentity.Of(http).PlayerId,
                PermissionNodes.Admin.ROOMS_VIEW,
                http.RequestAborted
            )
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }
}
