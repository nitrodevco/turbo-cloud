using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Assets;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The broken assets page (<c>/api/assets/broken</c>): every asset players can't load, with the
/// fixes that apply, for staff with <c>admin.gamedata.view</c>; ignoring one (with a note) and
/// bringing it back need <c>gamedata.manage</c> as well. The fixes themselves are the asset and
/// catalog picture endpoints' own (sync, upload, publish).
/// </summary>
internal sealed class BrokenAssetEndpoints(IGrainFactory grainFactory, AdminBrokenAssets broken)
{
    private const string NO_ACCESS = "You can't see the asset bundles.";
    private const string NO_MANAGE = "You can't change the asset bundles.";
    private const string NO_SUCH = "Name the source and the asset.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/assets/broken")
            .RequirePermission(PermissionNodes.Admin.GAMEDATA_VIEW, NO_ACCESS)
            .RequirePermissionToChange(PermissionNodes.Gamedata.MANAGE, NO_MANAGE);

        group.MapGet("/", ListAsync);
        group.MapPost("/ignore", IgnoreAsync);
        group.MapPost("/unignore", UnignoreAsync);
    }

    private async Task<IResult> ListAsync(CancellationToken ct) =>
        Results.Ok(await broken.ListAsync(ids => NamesAsync(ids, ct), ct).ConfigureAwait(false));

    private async Task<IResult> IgnoreAsync(
        BrokenAssetIgnoreInput input,
        HttpContext http,
        CancellationToken ct
    )
    {
        try
        {
            return await broken
                .IgnoreAsync(
                    input.Source ?? "",
                    input.Key ?? "",
                    input.Note ?? "",
                    AdminIdentity.Of(http).PlayerId,
                    ct
                )
                .ConfigureAwait(false)
                ? Results.NoContent()
                : AdminResults.Error(StatusCodes.Status400BadRequest, NO_SUCH);
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    private async Task<IResult> UnignoreAsync(
        BrokenAssetIgnoreInput input,
        HttpContext http,
        CancellationToken ct
    ) =>
        await broken
            .UnignoreAsync(input.Source ?? "", input.Key ?? "", AdminIdentity.Of(http).PlayerId, ct)
            .ConfigureAwait(false)
            ? Results.NoContent()
            : AdminResults.Error(StatusCodes.Status404NotFound, "It is not ignored.");

    /// <summary>The names of the staff who ignored assets, by id.</summary>
    private async Task<IReadOnlyDictionary<int, string>?> NamesAsync(
        IEnumerable<int> playerIds,
        CancellationToken ct
    )
    {
        var ids = playerIds.Where(x => x > 0).Distinct().Select(x => new PlayerId(x)).ToList();

        if (ids.Count == 0)
            return null;

        var names = await grainFactory
            .GetPlayerDirectoryGrain()
            .GetPlayerNamesAsync(ids, ct)
            .ConfigureAwait(false);

        return names?.ToDictionary(x => x.Key.Value, x => x.Value);
    }
}
