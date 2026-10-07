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

/// <summary>The room chat log, for staff with <c>admin.chatlog.view</c>. Read only.</summary>
internal sealed class ChatlogEndpoints(IGrainFactory grainFactory, AdminChatlogQueries chatlog)
{
    private const string NO_ACCESS = "You can't read the chat log.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured.MapGroup("/chatlog").AddEndpointFilter(RequireViewAsync);

        group.MapGet("/", SearchAsync);
        group.MapGet("/{id:int}/context", AroundAsync);
    }

    private async Task<IResult> SearchAsync(
        string? player,
        int? room,
        string? text,
        int? before,
        int? after,
        CancellationToken ct
    ) =>
        Results.Ok(
            await chatlog.SearchAsync(player, room, text, before, after, ct).ConfigureAwait(false)
        );

    private async Task<IResult> AroundAsync(int id, CancellationToken ct) =>
        Results.Ok(await chatlog.AroundAsync(id, ct).ConfigureAwait(false));

    private async ValueTask<object?> RequireViewAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var http = context.HttpContext;

        return await grainFactory
            .HasPermissionAsync(
                AdminIdentity.Of(http).PlayerId,
                PermissionNodes.Admin.CHATLOG_VIEW,
                http.RequestAborted
            )
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }
}
