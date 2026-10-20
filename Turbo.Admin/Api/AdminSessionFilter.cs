using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Orleans;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// Lets a request through only with a live session whose player still holds
/// <c>admin.panel</c>. The node is asked on every request, not remembered with the session, so
/// taking it away locks the panel at once.
/// </summary>
internal sealed class AdminSessionFilter(IGrainFactory grainFactory) : IEndpointFilter
{
    private const string BEARER = "Bearer ";

    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var http = context.HttpContext;
        var ct = http.RequestAborted;
        var header = http.Request.Headers.Authorization.ToString();

        if (!header.StartsWith(BEARER, StringComparison.OrdinalIgnoreCase))
            return Results.Unauthorized();

        var token = header[BEARER.Length..].Trim();
        var session = await grainFactory
            .GetAdminAuthGrain()
            .GetSessionAsync(token, ct)
            .ConfigureAwait(false);

        if (session is null)
            return Results.Unauthorized();

        if (
            !await grainFactory
                .HasPermissionAsync(session.PlayerId, PermissionNodes.Admin.PANEL, ct)
                .ConfigureAwait(false)
        )
            return Results.StatusCode(StatusCodes.Status403Forbidden);

        var name = await grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNameAsync(session.PlayerId, ct)
            .ConfigureAwait(false);

        new AdminIdentity(session.PlayerId, name, token, session.ExpiresAtUtc).AttachTo(http);

        if (await RefusalAsync(http, session.PlayerId, ct).ConfigureAwait(false) is { } refusal)
            return AdminResults.Error(StatusCodes.Status403Forbidden, refusal);

        return await next(context).ConfigureAwait(false);
    }

    /// <summary>
    /// The first <see cref="AdminAccess"/> the route declares that the staff member does not
    /// meet: the group's before the route's, so a staff member who may not see an area is told
    /// that rather than that they may not change it.
    /// </summary>
    private async Task<string?> RefusalAsync(
        HttpContext http,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        if (http.GetEndpoint()?.Metadata is not { } metadata)
            return null;

        var changes =
            !HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method);
        var exempt =
            metadata.GetMetadata<AdminAccessNote>() is
            { Kind: AdminAccessNoteKind.ViewIsEnough or AdminAccessNoteKind.ChecksInHandler };

        foreach (
            var access in metadata
                .GetOrderedMetadata<AdminAccess>()
                .OrderBy(x => x.OnRoute)
                .ThenBy(x => x.ChangesOnly)
        )
        {
            if (access.ChangesOnly && (!changes || exempt))
                continue;

            if (!await HoldsAnyAsync(playerId, access, ct).ConfigureAwait(false))
                return access.Refusal;
        }

        return null;
    }

    private async Task<bool> HoldsAnyAsync(
        PlayerId playerId,
        AdminAccess access,
        CancellationToken ct
    )
    {
        foreach (var node in access.AnyOf)
            if (await grainFactory.HasPermissionAsync(playerId, node, ct).ConfigureAwait(false))
                return true;

        return false;
    }
}
