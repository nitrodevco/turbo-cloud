using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Permissions;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The questions that are about no one group or player: which nodes exist, who is given a node,
/// and what has changed lately. The panel's <c>perm search</c> and <c>perm log</c>.
/// </summary>
internal sealed class PermissionLookupEndpoints(IGrainFactory grainFactory, PermissionViews views)
{
    private const int DEFAULT_COUNT = 50;

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/permissions")
            .AddEndpointFilter(PermissionResults.RequireView(grainFactory));

        group.MapGet("/catalog", () => Results.Ok(views.GetCatalog()));
        group.MapGet("/search", SearchAsync);
        group.MapGet("/log", LogAsync);
    }

    private async Task<IResult> SearchAsync(string? node, int? count, CancellationToken ct)
    {
        var trimmed = node?.Trim() ?? string.Empty;

        if (!PermissionNodeFormat.IsValidNode(trimmed))
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                "Name a node: lowercase dotted segments, like room.enter.locked."
            );

        return Results.Ok(
            await views.FindHoldersAsync(trimmed, count ?? DEFAULT_COUNT, ct).ConfigureAwait(false)
        );
    }

    private async Task<IResult> LogAsync(string? search, int? count, CancellationToken ct) =>
        Results.Ok(
            await views
                .NameAuditAsync(
                    await grainFactory
                        .GetPermissionGroupDirectoryGrain()
                        .GetRecentAuditAsync(
                            string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                            count ?? DEFAULT_COUNT,
                            ct
                        )
                        .ConfigureAwait(false),
                    ct
                )
                .ConfigureAwait(false)
        );
}
