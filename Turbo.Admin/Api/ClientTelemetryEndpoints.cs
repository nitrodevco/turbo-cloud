using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Turbo.Admin.Telemetry;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The Client health and Usage pages, for staff with <c>admin.telemetry.view</c>: what clients
/// report about how they run and the events they log (<see cref="AdminClientTelemetry"/>), by day,
/// browser, room and event, down to one player's or room's reports. Read only.
/// </summary>
internal sealed class ClientTelemetryEndpoints(AdminClientTelemetry telemetry)
{
    private const string NO_ACCESS = "You can't see client health and usage.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/telemetry")
            .RequirePermission(PermissionNodes.Admin.TELEMETRY_VIEW, NO_ACCESS);

        group.MapGet("/health", HealthAsync);
        group.MapGet("/usage", UsageAsync);
        group.MapGet("/funnel", FunnelAsync);
        group.MapGet("/players/{playerId:int}", PlayerAsync);
        group.MapGet("/rooms/{roomId:int}", RoomAsync);
    }

    private async Task<IResult> HealthAsync(
        DateOnly? from,
        DateOnly? to,
        string? browser,
        string? os,
        string? device,
        int? room,
        CancellationToken ct
    ) =>
        Results.Ok(
            await telemetry
                .HealthAsync(from, to, browser, os, device, room, ct)
                .ConfigureAwait(false)
        );

    private async Task<IResult> UsageAsync(
        DateOnly? from,
        DateOnly? to,
        string? category,
        CancellationToken ct
    ) => Results.Ok(await telemetry.UsageAsync(from, to, category, ct).ConfigureAwait(false));

    private async Task<IResult> FunnelAsync(
        DateOnly? from,
        DateOnly? to,
        string[]? step,
        CancellationToken ct
    ) => Results.Ok(await telemetry.FunnelAsync(from, to, step ?? [], ct).ConfigureAwait(false));

    private async Task<IResult> PlayerAsync(
        int playerId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct
    ) => Results.Ok(await telemetry.PlayerAsync(playerId, from, to, ct).ConfigureAwait(false));

    private async Task<IResult> RoomAsync(
        int roomId,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct
    ) => Results.Ok(await telemetry.RoomAsync(roomId, from, to, ct).ConfigureAwait(false));
}
