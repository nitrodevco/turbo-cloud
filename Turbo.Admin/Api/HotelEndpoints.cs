using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// Acting on the whole hotel from the dashboard: a hotel alert, maintenance and shutdown. Each is
/// the hotel's own command, written by <see cref="HotelActionLine"/> and run as the staff member
/// through <see cref="PanelCommands"/>: its node, its countdown, its confirmation and the command
/// log, as in game.
/// </summary>
internal sealed class HotelEndpoints(
    IGrainFactory grainFactory,
    ICommandRegistryProvider registryProvider,
    IOperatorCommandRunner runner
)
{
    public void Map(RouteGroupBuilder secured)
    {
        secured.MapGet("/hotel/abilities", AbilitiesAsync);
        secured.MapPost("/hotel/actions", ActAsync);
    }

    private async Task<IResult> AbilitiesAsync(HttpContext http, CancellationToken ct)
    {
        var resolved = await grainFactory
            .GetPlayerPermissionGrain(AdminIdentity.Of(http).PlayerId)
            .GetResolvedAsync(ct)
            .ConfigureAwait(false);

        return Results.Ok(
            new HotelAbilities(
                resolved.Has(PermissionNodes.Command.HOTELALERT),
                resolved.Has(PermissionNodes.Command.MAINTENANCE),
                resolved.Has(PermissionNodes.Command.SHUTDOWN)
            )
        );
    }

    private async Task<IResult> ActAsync(HttpContext http, HotelActionRequest request)
    {
        var (line, error) = HotelActionLine.Build(request);

        if (line is null)
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                error ?? "That can't be done."
            );

        return Results.Ok(
            await new PanelCommands(grainFactory, registryProvider, runner)
                .RunAsync(AdminIdentity.Of(http), line)
                .ConfigureAwait(false)
        );
    }
}
