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
/// Players' private messages, for staff with <c>admin.messages.view</c>: one player's, or the
/// conversation between two. Read only.
/// </summary>
internal sealed class MessageLogEndpoints(IGrainFactory grainFactory, AdminMessageQueries messages)
{
    private const string NO_ACCESS = "You can't read private messages.";

    public void Map(RouteGroupBuilder secured) =>
        secured.MapGroup("/messages").AddEndpointFilter(RequireViewAsync).MapGet("/", SearchAsync);

    private async Task<IResult> SearchAsync(
        string? player,
        string? with,
        string? text,
        int? before,
        int? after,
        CancellationToken ct
    ) =>
        Results.Ok(
            await messages.SearchAsync(player, with, text, before, after, ct).ConfigureAwait(false)
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
                PermissionNodes.Admin.MESSAGES_VIEW,
                http.RequestAborted
            )
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }
}
