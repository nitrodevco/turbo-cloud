using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Turbo.Admin.Emulator.Endpoints;

public static class EmulatorEndpoints
{
    public static IEndpointRouteBuilder MapAdminEmulatorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin/emulator").RequireAuthorization();

        group.MapGet(
            "/status",
            (IEmulatorProcessSupervisor supervisor) =>
                Results.Ok(new EmulatorStatusResponse(supervisor.Status.ToString()))
        );

        group.MapPost(
            "/start",
            async (IEmulatorProcessSupervisor supervisor, CancellationToken ct) =>
            {
                await supervisor.StartAsync(ct).ConfigureAwait(false);

                return Results.Ok(new EmulatorStatusResponse(supervisor.Status.ToString()));
            }
        );

        group.MapPost(
            "/stop",
            async (IEmulatorProcessSupervisor supervisor, CancellationToken ct) =>
            {
                await supervisor.StopAsync(ct).ConfigureAwait(false);

                return Results.Ok(new EmulatorStatusResponse(supervisor.Status.ToString()));
            }
        );

        group.MapPost(
            "/restart",
            async (IEmulatorProcessSupervisor supervisor, CancellationToken ct) =>
            {
                await supervisor.RestartAsync(ct).ConfigureAwait(false);

                return Results.Ok(new EmulatorStatusResponse(supervisor.Status.ToString()));
            }
        );

        return app;
    }
}

internal sealed record EmulatorStatusResponse(string Status);
