using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Commands;
using Turbo.Database.Entities.Hotel;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// Acting on the whole hotel from the dashboard: a hotel alert, maintenance and shutdown. Each is
/// the hotel's own command, written by <see cref="HotelActionLine"/> and run as the staff member
/// through <see cref="PanelCommands"/>: its node, its countdown, its confirmation and the command
/// log, as in game. The welcome message shown at login is read and saved here too, for staff with
/// <c>admin.welcome.manage</c>.
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
        secured
            .MapGet("/hotel/welcome-message", GetWelcomeMessageAsync)
            .AddEndpointFilter(RequireWelcomeMessageAsync);
        secured
            .MapPut("/hotel/welcome-message", SetWelcomeMessageAsync)
            .AddEndpointFilter(RequireWelcomeMessageAsync);
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
                resolved.Has(PermissionNodes.Command.SHUTDOWN),
                resolved.Has(PermissionNodes.Admin.WELCOME_MESSAGE_MANAGE)
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

    private async Task<IResult> GetWelcomeMessageAsync(CancellationToken ct) =>
        Results.Ok(
            new WelcomeMessageResponse(
                await grainFactory
                    .GetWelcomeMessageGrain()
                    .GetMessageAsync(ct)
                    .ConfigureAwait(false),
                HotelSettingEntity.VALUE_MAX_LENGTH
            )
        );

    private async Task<IResult> SetWelcomeMessageAsync(
        WelcomeMessageRequest request,
        CancellationToken ct
    )
    {
        var message = (request.Message ?? string.Empty).Replace("\r\n", "\n").Trim();

        if (message.Length > HotelSettingEntity.VALUE_MAX_LENGTH)
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                $"The welcome message can be at most {HotelSettingEntity.VALUE_MAX_LENGTH} characters."
            );

        return Results.Ok(
            new WelcomeMessageResponse(
                await grainFactory
                    .GetWelcomeMessageGrain()
                    .SetMessageAsync(message, ct)
                    .ConfigureAwait(false),
                HotelSettingEntity.VALUE_MAX_LENGTH
            )
        );
    }

    private async ValueTask<object?> RequireWelcomeMessageAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var http = context.HttpContext;

        return await grainFactory
            .HasPermissionAsync(
                AdminIdentity.Of(http).PlayerId,
                PermissionNodes.Admin.WELCOME_MESSAGE_MANAGE,
                http.RequestAborted
            )
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(
                StatusCodes.Status403Forbidden,
                "You can't change the welcome message."
            );
    }
}
