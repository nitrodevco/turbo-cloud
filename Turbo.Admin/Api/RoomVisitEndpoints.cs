using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Turbo.Admin.Rooms;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The room visits log, for staff with <c>admin.players.view</c>: who went into which room is
/// what a player's page says about them. Read only.
/// </summary>
internal sealed class RoomVisitEndpoints(AdminRoomVisits visits)
{
    private const string NO_ACCESS = "You can't look up players.";

    public void Map(RouteGroupBuilder secured) =>
        secured
            .MapGet("/room-visits", SearchAsync)
            .RequirePermission(PermissionNodes.Admin.PLAYERS_VIEW, NO_ACCESS);

    private async Task<IResult> SearchAsync(
        string? player,
        int? room,
        int? page,
        string? sort,
        string? dir,
        CancellationToken ct
    ) =>
        Results.Ok(
            await visits.SearchAsync(player, room, page ?? 1, sort, dir, ct).ConfigureAwait(false)
        );
}
