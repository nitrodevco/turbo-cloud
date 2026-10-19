using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Orleans;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// Who may see and change the game's content, for the content endpoints outside
/// <see cref="ContentEndpoints"/>: seeing needs <c>admin.content.view</c>, changing
/// <c>content.manage</c> as well, and a change the game refuses answers 400 with why.
/// </summary>
internal sealed class ContentAccess(IGrainFactory grainFactory)
{
    private const string NO_ACCESS = "You can't see the game's content.";
    private const string NO_MANAGE = "You can't change the game's content.";

    /// <summary>Who made a change, for the log: the panel and the staff member's name.</summary>
    public static string Actor(HttpContext http) => $"panel:{AdminIdentity.Of(http).Name}";

    public Task<bool> CanManageAsync(HttpContext http, CancellationToken ct) =>
        grainFactory.HasPermissionAsync(
            AdminIdentity.Of(http).PlayerId,
            PermissionNodes.Content.MANAGE,
            ct
        );

    /// <summary>A change: what it answers, 403 without <c>content.manage</c>, or 400 with why it was refused.</summary>
    public async Task<IResult> ManageAsync(
        HttpContext http,
        CancellationToken ct,
        Func<Task<IResult>> change
    )
    {
        if (!await CanManageAsync(http, ct).ConfigureAwait(false))
            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);

        try
        {
            return await change().ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    /// <summary>The endpoint filter: only staff with <c>admin.content.view</c> go on.</summary>
    public async ValueTask<object?> RequireViewAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var http = context.HttpContext;

        return await grainFactory
            .HasPermissionAsync(
                AdminIdentity.Of(http).PlayerId,
                PermissionNodes.Admin.CONTENT_VIEW,
                http.RequestAborted
            )
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }
}
