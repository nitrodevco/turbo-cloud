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
        var group = secured
            .MapGroup("/word-filter")
            .RequirePermission(PermissionNodes.Moderation.TOOL, NO_ACCESS)
            .RequirePermissionToChange(PermissionNodes.Moderation.FILTER_MANAGE, NO_MANAGE);

        group.MapGet("/", ListAsync);
        group.MapPost("/", AddAsync);
        group.MapDelete("/{id:int}", RemoveAsync);
        group
            .MapPost(
                "/test",
                (WordFilterTestRequest request) => Results.Ok(words.Test(request.Text))
            )
            .ViewIsEnough("Trying a line on the filter changes nothing.");
    }

    private async Task<IResult> ListAsync(HttpContext http, CancellationToken ct) =>
        Results.Ok(
            await words
                .ListAsync(
                    await grainFactory
                        .HoldsAsync(http, PermissionNodes.Moderation.FILTER_MANAGE, ct)
                        .ConfigureAwait(false),
                    ct
                )
                .ConfigureAwait(false)
        );

    private async Task<IResult> AddAsync(
        WordFilterAddRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        Results.Ok(
            await words
                .AddAsync(AdminIdentity.Of(http).PlayerId, request.Words, ct)
                .ConfigureAwait(false)
        );

    private async Task<IResult> RemoveAsync(int id, HttpContext http, CancellationToken ct) =>
        await words.RemoveAsync(AdminIdentity.Of(http).PlayerId, id, ct).ConfigureAwait(false)
            ? Results.NoContent()
            : AdminResults.Error(StatusCodes.Status404NotFound, NOT_FOUND);
}
