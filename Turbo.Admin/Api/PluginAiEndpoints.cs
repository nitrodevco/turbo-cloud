using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Plugins;

namespace Turbo.Admin.Api;

/// <summary>
/// The plugins' language models (<c>docs/ai-sdk.md</c>), for staff with <c>admin.plugins.view</c>:
/// the providers (whether each has a key, never the key), the failover order, the plugins declaring
/// AI with their purpose and whether the hotel allowed them, and the usage log summed over the last
/// <c>days</c>. The providers, keys and allowed plugins are changed on the Settings page
/// (<c>Turbo:Ai</c>).
/// </summary>
internal sealed class PluginAiEndpoints(IServiceProvider services)
{
    private const string NO_ACCESS = "You can't see the server's plugins.";

    public void Map(RouteGroupBuilder secured)
    {
        secured
            .MapGroup("/plugins/ai")
            .RequirePermission(PermissionNodes.Admin.PLUGINS_VIEW, NO_ACCESS)
            .MapGet("/", GetAsync);
    }

    private async Task<IResult> GetAsync(int? days, CancellationToken ct) =>
        services.GetService(typeof(IAiOverview)) is IAiOverview overview
            ? Results.Ok(await overview.GetAsync(days ?? 30, ct).ConfigureAwait(false))
            : Results.NoContent();
}
