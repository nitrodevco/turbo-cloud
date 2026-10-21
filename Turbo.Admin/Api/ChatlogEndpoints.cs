using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Turbo.Admin.Rooms;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>The room chat log, for staff with <c>admin.chatlog.view</c>. Read only.</summary>
internal sealed class ChatlogEndpoints(AdminChatlogQueries chatlog)
{
    private const string NO_ACCESS = "You can't read the chat log.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/chatlog")
            .RequirePermission(PermissionNodes.Admin.CHATLOG_VIEW, NO_ACCESS);

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
}
