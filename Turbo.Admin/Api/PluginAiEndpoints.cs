using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Turbo.Admin.Api.Contracts;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Plugins;

namespace Turbo.Admin.Api;

/// <summary>
/// The plugins' language models (<c>docs/ai-sdk.md</c>), for staff with <c>admin.plugins.view</c>:
/// the providers (whether each has a key, never the key), the failover order, the plugins declaring
/// AI with their purpose and whether the hotel allowed them, and the usage log summed by day over
/// the last <c>days</c>. With <c>settings.manage</c> as well: a provider's key set or removed
/// (written only, never read back) and a provider tried with a one-token call. The rest of
/// <c>Turbo:Ai</c> is changed through the settings API.
/// </summary>
internal sealed class PluginAiEndpoints(IServiceProvider services)
{
    private const string NO_ACCESS = "You can't see the server's plugins.";
    private const string NO_MANAGE = "You can't change the server's settings.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/plugins/ai")
            .RequirePermission(PermissionNodes.Admin.PLUGINS_VIEW, NO_ACCESS)
            .RequirePermissionToChange(PermissionNodes.Settings.MANAGE, NO_MANAGE);

        group.MapGet("/", GetAsync);
        group.MapPut("/providers/{provider}/key", SetKeyAsync);
        group.MapPost("/providers/{provider}/test", TestAsync);
    }

    private IAiOverview? Overview => services.GetService(typeof(IAiOverview)) as IAiOverview;

    private async Task<IResult> GetAsync(int? days, CancellationToken ct) =>
        Overview is { } overview
            ? Results.Ok(await overview.GetAsync(days ?? 30, ct).ConfigureAwait(false))
            : Results.NoContent();

    private async Task<IResult> SetKeyAsync(
        string provider,
        AiKeyRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        if (Overview is not { } overview)
            return Results.NotFound();

        try
        {
            await overview
                .SetKeyAsync(provider, request.Key, AdminIdentity.Of(http).PlayerId, ct)
                .ConfigureAwait(false);

            return Results.NoContent();
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    private async Task<IResult> TestAsync(string provider, CancellationToken ct) =>
        Overview is { } overview
            ? Results.Ok(await overview.TestAsync(provider, ct).ConfigureAwait(false))
            : Results.NotFound();
}
