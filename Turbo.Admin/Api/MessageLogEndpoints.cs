using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Turbo.Admin.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// Players' private messages, for staff with <c>admin.messages.view</c>: one player's, or the
/// conversation between two. Read only.
/// </summary>
internal sealed class MessageLogEndpoints(AdminMessageQueries messages)
{
    private const string NO_ACCESS = "You can't read private messages.";

    public void Map(RouteGroupBuilder secured) =>
        secured
            .MapGroup("/messages")
            .RequirePermission(PermissionNodes.Admin.MESSAGES_VIEW, NO_ACCESS)
            .MapGet("/", SearchAsync);

    private async Task<IResult> SearchAsync(
        string? player,
        string? with,
        string? text,
        int? page,
        string? sort,
        string? dir,
        CancellationToken ct
    ) =>
        Results.Ok(
            await messages
                .SearchAsync(player, with, text, page ?? 1, sort, dir, ct)
                .ConfigureAwait(false)
        );
}
