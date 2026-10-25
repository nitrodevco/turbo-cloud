using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Turbo.Admin.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>The command log, for staff with <c>admin.commandlog.view</c>. Read only.</summary>
internal sealed class CommandLogEndpoints(AdminCommandLogQueries commandLog)
{
    private const string NO_ACCESS = "You can't read the command log.";

    public void Map(RouteGroupBuilder secured) =>
        secured
            .MapGet("/command-log", SearchAsync)
            .RequirePermission(PermissionNodes.Admin.COMMAND_LOG_VIEW, NO_ACCESS);

    private async Task<IResult> SearchAsync(
        string? player,
        string? command,
        string? outcome,
        string? source,
        int? page,
        string? sort,
        string? dir,
        CancellationToken ct
    ) =>
        Results.Ok(
            await commandLog
                .SearchAsync(player, command, outcome, source, page ?? 1, sort, dir, ct)
                .ConfigureAwait(false)
        );
}
