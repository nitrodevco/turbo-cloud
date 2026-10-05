using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;
using Turbo.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Texts;

namespace Turbo.Admin.Api;

/// <summary>
/// The panel's command console: the operator commands the signed-in player may run, and running
/// one. A line runs exactly as it would typed in game by the same player (nodes, rate limits,
/// confirmation, the command log), through the runner and a <see cref="WebOperatorExecutor"/>.
/// </summary>
internal sealed class CommandEndpoints(
    IGrainFactory grainFactory,
    ICommandRegistryProvider registryProvider,
    IOperatorCommandRunner runner,
    IHotelTextProvider texts,
    IOptions<AdminConfig> config
)
{
    public void Map(RouteGroupBuilder secured)
    {
        secured.MapGet("/commands", ListAsync);
        secured.MapPost("/commands/run", RunAsync);
    }

    private async Task<IResult> ListAsync(HttpContext http, CancellationToken ct)
    {
        var identity = AdminIdentity.Of(http);
        var resolved = await grainFactory
            .GetPlayerPermissionGrain(identity.PlayerId)
            .GetResolvedAsync(ct)
            .ConfigureAwait(false);
        var registry = registryProvider.Current;

        return Results.Ok(
            registry
                .Commands.Where(x => x.Nodes.Count == 0 || x.Nodes.Any(resolved.Has))
                .OrderBy(x => x.Category, StringComparer.Ordinal)
                .ThenBy(x => x.Name, StringComparer.Ordinal)
                .Select(x => new CommandInfo(
                    x.Name,
                    registry.AliasesOf(x),
                    x.Description,
                    x.Category,
                    !x.IsOperator,
                    CommandHelp.Describe(registry, x, resolved.Has, texts, details: true)
                ))
                .ToList()
        );
    }

    private async Task<IResult> RunAsync(HttpContext http, RunCommandRequest request)
    {
        var line = (request.Line ?? string.Empty).Trim();

        if (line.Length == 0 || line.Length > config.Value.MaxCommandLength)
            return Results.BadRequest();

        return Results.Ok(
            await new PanelCommands(grainFactory, registryProvider, runner)
                .RunAsync(AdminIdentity.Of(http), line)
                .ConfigureAwait(false)
        );
    }
}
