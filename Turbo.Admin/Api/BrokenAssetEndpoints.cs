using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Assets;
using Turbo.Admin.Catalog;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The broken assets page (<c>/api/assets/broken</c>): every asset players can't load, with the
/// fixes that apply, for staff with <c>admin.gamedata.view</c>; ignoring one (with a note) and
/// bringing it back need <c>gamedata.manage</c> as well. The fixes themselves are the asset and
/// catalog picture endpoints' own (sync, upload, publish), but for a badge's picture, which had no
/// upload: <c>POST /assets/broken/badge-picture</c>.
/// </summary>
internal sealed class BrokenAssetEndpoints(IGrainFactory grainFactory, AdminBrokenAssets broken)
{
    private const string NO_ACCESS = "You can't see the asset bundles.";
    private const string NO_MANAGE = "You can't change the asset bundles.";
    private const string NO_SUCH = "Name the source and the asset.";

    // Room for the upload form's other fields and boundaries, beside the file itself.
    private const long FORM_OVERHEAD_BYTES = 64 * 1024;

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/assets/broken")
            .RequirePermission(PermissionNodes.Admin.GAMEDATA_VIEW, NO_ACCESS)
            .RequirePermissionToChange(PermissionNodes.Gamedata.MANAGE, NO_MANAGE);

        group.MapGet("/", ListAsync);
        group.MapPost("/ignore", IgnoreAsync);
        group.MapPost("/unignore", UnignoreAsync);
        group.MapPost("/badge-picture", UploadBadgeAsync);
    }

    /// <summary>A badge's missing picture, as a form with <c>code</c> and <c>file</c> (a GIF or a PNG).</summary>
    private async Task<IResult> UploadBadgeAsync(HttpContext http, CancellationToken ct)
    {
        if (!http.Request.HasFormContentType)
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                "Send the picture as a form."
            );

        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = AdminCatalogImages.UPLOAD_MAX_BYTES + FORM_OVERHEAD_BYTES;

        IFormCollection form;

        try
        {
            form = await http.Request.ReadFormAsync(ct).ConfigureAwait(false);
        }
        catch (BadHttpRequestException ex)
            when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, "The picture is too large.");
        }
        catch (InvalidDataException ex)
        {
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                $"The form could not be read: {ex.Message}"
            );
        }

        if (form.Files.GetFile("file") is not { } file)
            return AdminResults.Error(StatusCodes.Status400BadRequest, "Choose a picture.");

        if (file.Length > AdminCatalogImages.UPLOAD_MAX_BYTES)
            return AdminResults.Error(StatusCodes.Status400BadRequest, "The picture is too large.");

        byte[] data;

        using (var memory = new MemoryStream((int)file.Length))
        {
            var stream = file.OpenReadStream();
            await using var streamScope = stream.ConfigureAwait(false);

            await stream.CopyToAsync(memory, ct).ConfigureAwait(false);
            data = memory.ToArray();
        }

        try
        {
            await broken
                .UploadBadgeAsync(
                    form["code"].ToString(),
                    file.FileName,
                    data,
                    AdminIdentity.Of(http).PlayerId,
                    ct
                )
                .ConfigureAwait(false);

            return Results.NoContent();
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
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
