using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>The command log, for staff with <c>admin.commandlog.view</c>. Read only.</summary>
internal sealed class CommandLogEndpoints(
    IGrainFactory grainFactory,
    AdminCommandLogQueries commandLog
)
{
    private const string NO_ACCESS = "You can't read the command log.";

    public void Map(RouteGroupBuilder secured) =>
        secured.MapGet("/command-log", SearchAsync).AddEndpointFilter(RequireViewAsync);

    private async Task<IResult> SearchAsync(
        string? player,
        string? command,
        string? outcome,
        string? source,
        int? page,
        CancellationToken ct
    ) =>
        Results.Ok(
            await commandLog
                .SearchAsync(player, command, outcome, source, page ?? 1, ct)
                .ConfigureAwait(false)
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
                PermissionNodes.Admin.COMMAND_LOG_VIEW,
                http.RequestAborted
            )
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }
}
