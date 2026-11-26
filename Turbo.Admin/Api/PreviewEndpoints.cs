using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Permissions;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// Viewing the panel and the hotel as a group does: the staff member's own preview, started from a
/// group's page and ended from the banner every page shows while it runs (<c>docs/admin-panel.md</c>,
/// <i>Viewing as a group</i>). The permission grain holds it and decides whether it may start
/// (<see cref="PermissionPreview"/>), so starting one needs only the Permissions page it is
/// started from, and ending one needs nothing: <see cref="AdminSessionFilter"/> lets a previewing
/// staff member in on their own <c>admin.panel</c>.
/// </summary>
internal sealed class PreviewEndpoints(IGrainFactory grainFactory)
{
    public void Map(RouteGroupBuilder secured)
    {
        var preview = secured.MapGroup("/me/preview");

        preview
            .MapPut("", StartAsync)
            .RequirePermission(PermissionNodes.Admin.PERMISSIONS_VIEW, PermissionResults.NO_ACCESS);
        preview
            .MapDelete("", EndAsync)
            .PanelOnly("Ends the caller's own preview, giving them back their own permissions.");
    }

    private async Task<IResult> StartAsync(
        HttpContext http,
        StartPermissionPreviewRequest request,
        CancellationToken ct
    )
    {
        var group = request.Group?.Trim().ToLowerInvariant() ?? string.Empty;
        var result = await grainFactory
            .GetPlayerPermissionGrain(AdminIdentity.Of(http).PlayerId)
            .StartPreviewAsync(group, ct)
            .ConfigureAwait(false);

        return result.Result switch
        {
            PermissionPreviewResultType.Started => Results.Ok(
                PermissionViews.PreviewOf(result.Preview)
            ),
            PermissionPreviewResultType.UnknownGroup => AdminResults.Error(
                StatusCodes.Status404NotFound,
                PermissionResults.PreviewRefusalOf(result, group)!
            ),
            _ => AdminResults.Error(
                StatusCodes.Status403Forbidden,
                PermissionResults.PreviewRefusalOf(result, group)!
            ),
        };
    }

    private async Task<IResult> EndAsync(HttpContext http, CancellationToken ct)
    {
        await grainFactory
            .GetPlayerPermissionGrain(AdminIdentity.Of(http).PlayerId)
            .EndPreviewAsync(ct)
            .ConfigureAwait(false);

        return Results.NoContent();
    }
}
