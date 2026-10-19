using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Moderation;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The hotel's word filter, for staff with <c>moderation.tool</c>: the words, and trying a line on
/// them. Adding and taking words off needs <c>moderation.filter.manage</c> as well.
/// </summary>
internal sealed class WordFilterEndpoints(IGrainFactory grainFactory, AdminWordFilter words)
{
    private const string NO_ACCESS = "You can't see the word filter.";
    private const string NO_MANAGE = "You can't change the word filter.";
    private const string NOT_FOUND = "That word isn't on the filter.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured.MapGroup("/word-filter").AddEndpointFilter(RequireToolAsync);

        group.MapGet("/", ListAsync);
        group.MapPost("/", AddAsync);
        group.MapDelete("/{id:int}", RemoveAsync);
        group.MapPost(
            "/test",
            (WordFilterTestRequest request) => Results.Ok(words.Test(request.Text))
        );
    }

    private async Task<IResult> ListAsync(HttpContext http, CancellationToken ct) =>
        Results.Ok(
            await words
                .ListAsync(await CanManageAsync(http, ct).ConfigureAwait(false), ct)
                .ConfigureAwait(false)
        );

    private async Task<IResult> AddAsync(
        WordFilterAddRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        if (!await CanManageAsync(http, ct).ConfigureAwait(false))
            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);

        return Results.Ok(
            await words
                .AddAsync(AdminIdentity.Of(http).PlayerId, request.Words, ct)
                .ConfigureAwait(false)
        );
    }

    private async Task<IResult> RemoveAsync(int id, HttpContext http, CancellationToken ct)
    {
        if (!await CanManageAsync(http, ct).ConfigureAwait(false))
            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);

        return await words
            .RemoveAsync(AdminIdentity.Of(http).PlayerId, id, ct)
            .ConfigureAwait(false)
            ? Results.NoContent()
            : AdminResults.Error(StatusCodes.Status404NotFound, NOT_FOUND);
    }

    private Task<bool> CanManageAsync(HttpContext http, CancellationToken ct) =>
        grainFactory.HasPermissionAsync(
            AdminIdentity.Of(http).PlayerId,
            PermissionNodes.Moderation.FILTER_MANAGE,
            ct
        );

    private async ValueTask<object?> RequireToolAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var http = context.HttpContext;

        return await grainFactory
            .HasPermissionAsync(
                AdminIdentity.Of(http).PlayerId,
                PermissionNodes.Moderation.TOOL,
                http.RequestAborted
            )
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }
}
