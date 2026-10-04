using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Orleans;
using Turbo.Primitives.Orleans;
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

        return await next(context).ConfigureAwait(false);
    }
}
