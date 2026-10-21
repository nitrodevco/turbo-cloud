using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Content;

namespace Turbo.Admin.Api;

/// <summary>
/// The room polls (<c>/polls</c>) and what players answered, for staff with
/// <c>admin.content.view</c>; changing them needs <c>content.manage</c> as well.
/// </summary>
internal sealed class PollEndpoints(ContentAccess access, AdminPollEditor polls)
{
    private const string NOT_FOUND = "There is no such poll.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = ContentAccess.Require(secured.MapGroup("/polls"));

        group.MapGet(
            "/",
            async (HttpContext http, CancellationToken ct) =>
                Results.Ok(
                    await polls
                        .ListAsync(await access.CanManageAsync(http, ct).ConfigureAwait(false), ct)
                        .ConfigureAwait(false)
                )
        );
        group.MapGet(
            "/{id:int}",
            async (int id, CancellationToken ct) =>
                await polls.GetAsync(id, ct).ConfigureAwait(false) is { } poll
                    ? Results.Ok(poll)
                    : AdminResults.Error(StatusCodes.Status404NotFound, NOT_FOUND)
        );
        group.MapPost(
            "/",
            (PollRequest request, HttpContext http, CancellationToken ct) =>
                ContentAccess.ChangeAsync(async () =>
                    Results.Ok(
                        new
                        {
                            id = await polls
                                .SaveAsync(0, request, ContentAccess.Actor(http), ct)
                                .ConfigureAwait(false),
                        }
                    )
                )
        );
        group.MapPut(
            "/{id:int}",
            (int id, PollRequest request, HttpContext http, CancellationToken ct) =>
                ContentAccess.ChangeAsync(async () =>
                    Results.Ok(
                        new
                        {
                            id = await polls
                                .SaveAsync(id, request, ContentAccess.Actor(http), ct)
                                .ConfigureAwait(false),
                        }
                    )
                )
        );
        group.MapDelete(
            "/{id:int}",
            (int id, HttpContext http, CancellationToken ct) =>
                ContentAccess.ChangeAsync(async () =>
                    await polls.RemoveAsync(id, ContentAccess.Actor(http), ct).ConfigureAwait(false)
                        ? Results.NoContent()
                        : AdminResults.Error(StatusCodes.Status404NotFound, NOT_FOUND)
                )
        );
    }
}
