using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Settings;

namespace Turbo.Admin.Api;

/// <summary>
/// The server's settings - each value, where it comes from, and what waits on a restart - for
/// staff with <c>admin.settings.view</c>. Overriding one, replacing a secret and putting one back
/// need <c>settings.manage</c> as well. A secret's value is never sent.
/// </summary>
internal sealed class SettingsEndpoints(IGrainFactory grainFactory, IServerSettings settings)
{
    private const string NO_ACCESS = "You can't see the server's settings.";
    private const string NO_MANAGE = "You can't change the server's settings.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured.MapGroup("/settings").AddEndpointFilter(RequireViewAsync);

        group.MapGet("/", ListAsync);
        group.MapPut("/", SaveAsync);
        group.MapDelete("/", ResetAsync);
        group.MapGet("/history", (int? page, CancellationToken ct) => HistoryAsync(page, ct));
    }

    private async Task<IResult> ListAsync(HttpContext http, CancellationToken ct) =>
        Results.Ok(
            new SettingsResponse(
                settings.List(),
                await CanManageAsync(http, ct).ConfigureAwait(false)
            )
        );

    private Task<IResult> SaveAsync(
        SettingSaveRequest request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                try
                {
                    return Results.Ok(
                        await settings
                            .SaveAsync(
                                request.Path ?? string.Empty,
                                request.Value ?? string.Empty,
                                AdminIdentity.Of(http).PlayerId,
                                ct
                            )
                            .ConfigureAwait(false)
                    );
                }
                catch (ArgumentException ex)
                {
                    return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
                }
            }
        );

    private Task<IResult> ResetAsync(string? path, HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                try
                {
                    return await settings
                        .ResetAsync(path ?? string.Empty, AdminIdentity.Of(http).PlayerId, ct)
                        .ConfigureAwait(false)
                        ? Results.NoContent()
                        : Results.NotFound();
                }
                catch (ArgumentException ex)
                {
                    return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
                }
            }
        );

    private async Task<IResult> HistoryAsync(int? page, CancellationToken ct) =>
        Results.Ok(await settings.HistoryAsync(page ?? 0, ct).ConfigureAwait(false));

    /// <summary>A change by someone who holds <c>settings.manage</c>.</summary>
    private async Task<IResult> ManageAsync(
        HttpContext http,
        CancellationToken ct,
        Func<Task<IResult>> change
    ) =>
        await CanManageAsync(http, ct).ConfigureAwait(false)
            ? await change().ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);

    private Task<bool> CanManageAsync(HttpContext http, CancellationToken ct) =>
        grainFactory.HasPermissionAsync(
            AdminIdentity.Of(http).PlayerId,
            PermissionNodes.Settings.MANAGE,
            ct
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
                PermissionNodes.Admin.SETTINGS_VIEW,
                http.RequestAborted
            )
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }
}
