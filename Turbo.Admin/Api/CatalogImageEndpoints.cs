using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Catalog;
using Turbo.Admin.Media;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The catalog's pictures (<see cref="AdminCatalogImages"/>): listed, searched and drawn for staff
/// with <c>admin.catalog.view</c>; uploaded and deleted by those who also hold <c>catalog.manage</c>.
/// Its pages' icons (<c>/catalog/icons</c>) likewise: the files the media library indexes, reached
/// here under the catalog's own nodes.
/// </summary>
internal sealed class CatalogImageEndpoints(
    AdminCatalogImages images,
    MediaLibrary media,
    IAssetSyncService assets,
    ILogger<CatalogImageEndpoints> logger
)
{
    private const string NO_ACCESS = "You can't see the catalog.";
    private const string NO_MANAGE = "You can't change the catalog.";
    private const string NO_IMAGE = "There is no such picture.";
    private const string NO_ICON = "There is no such icon.";

    // Room for the upload form's other fields and boundaries, beside the file itself.
    private const long FORM_OVERHEAD_BYTES = 64 * 1024;

    // How much of a file tells a PNG from a GIF.
    private const int HEAD_BYTES = 8;

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/catalog/images")
            .RequirePermission(PermissionNodes.Admin.CATALOG_VIEW, NO_ACCESS)
            .RequirePermissionToChange(PermissionNodes.Catalog.MANAGE, NO_MANAGE);

        group.MapGet("/", ListAsync);
        group.MapGet("/{name}/file", FileAsync);
        group.MapPost("/", UploadAsync);
        group.MapDelete("/{name}", DeleteAsync);
        group.MapPost("/habbo", FetchFromHabbo);

        var icons = secured
            .MapGroup("/catalog/icons")
            .RequirePermission(PermissionNodes.Admin.CATALOG_VIEW, NO_ACCESS)
            .RequirePermissionToChange(PermissionNodes.Catalog.MANAGE, NO_MANAGE);

        icons.MapGet("/", ListIconsAsync);
        icons.MapGet("/{number:int}/file", IconFileAsync);
        icons.MapPost("/", UploadIconAsync);
        icons.MapDelete("/{number:int}", DeleteIconAsync);
    }

    private async Task<IResult> ListIconsAsync(
        string? q,
        string? show,
        int? page,
        int? size,
        CancellationToken ct
    )
    {
        var shown = (show ?? "").Trim().ToLowerInvariant();

        if (shown is not ("" or "all" or "unused"))
            return AdminResults.Error(StatusCodes.Status400BadRequest, "Show is all or unused.");

        return Results.Ok(
            await media
                .SearchAsync(
                    new MediaQuery(
                        q,
                        Kind: MediaLibrary.KIND_CATALOG_ICON,
                        Used: shown,
                        Page: page ?? 0,
                        Size: size ?? 60
                    ),
                    ct
                )
                .ConfigureAwait(false)
        );
    }

    private async Task<IResult> IconFileAsync(int number, CancellationToken ct) =>
        await media.ReadAsync(MediaLibrary.CatalogIconId(number), ct).ConfigureAwait(false)
            is { } file
            ? Results.File(file.Data, file.ContentType)
            : AdminResults.Error(StatusCodes.Status404NotFound, NO_ICON);

    /// <summary>A PNG kept as the icon of the form's <c>number</c>, replacing the one it has.</summary>
    private async Task<IResult> UploadIconAsync(HttpContext http, CancellationToken ct)
    {
        if (!http.Request.HasFormContentType)
            return AdminResults.Error(StatusCodes.Status400BadRequest, "Send the icon as a form.");

        var tooLarge = $"An icon is at most {MediaLibrary.UPLOAD_MAX_BYTES / (1024 * 1024)} MB.";

        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = MediaLibrary.UPLOAD_MAX_BYTES + FORM_OVERHEAD_BYTES;

        IFormCollection form;

        try
        {
            form = await http.Request.ReadFormAsync(ct).ConfigureAwait(false);
        }
        catch (BadHttpRequestException ex)
            when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, tooLarge);
        }
        catch (InvalidDataException ex)
        {
            logger.LogWarning(ex, "A catalog icon upload's form could not be read");

            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                $"The form could not be read: {ex.Message}"
            );
        }

        if (form.Files.GetFile("file") is not { } file)
            return AdminResults.Error(StatusCodes.Status400BadRequest, "Choose an icon.");

        if (!int.TryParse(form["number"].ToString(), out var number) || number <= 0)
            return AdminResults.Error(StatusCodes.Status400BadRequest, "Give the icon's number.");

        if (file.Length > MediaLibrary.UPLOAD_MAX_BYTES)
            return AdminResults.Error(StatusCodes.Status400BadRequest, tooLarge);

        byte[] data;

        using (var memory = new MemoryStream((int)file.Length))
        {
            var stream = file.OpenReadStream();
            await using var streamScope = stream.ConfigureAwait(false);

            await stream.CopyToAsync(memory, ct).ConfigureAwait(false);
            data = memory.ToArray();
        }

        var (icon, error) = await media
            .UploadCatalogIconAsync(number, data, AdminIdentity.Of(http).PlayerId, ct)
            .ConfigureAwait(false);

        return icon is not null
            ? Results.Ok(icon)
            : AdminResults.Error(
                StatusCodes.Status400BadRequest,
                error ?? "The icon was not kept."
            );
    }

    /// <summary>Deletes an icon no page shows; 409 with the pages while any does.</summary>
    private async Task<IResult> DeleteIconAsync(int number, HttpContext http, CancellationToken ct)
    {
        var outcome = await media
            .DeleteCatalogIconAsync(number, AdminIdentity.Of(http).PlayerId, ct)
            .ConfigureAwait(false);

        return outcome switch
        {
            { Found: false } => AdminResults.Error(StatusCodes.Status404NotFound, NO_ICON),
            { UsedOn: { } uses, Error: { } error } => Results.Json(
                new MediaInUseResponse(error, uses),
                statusCode: StatusCodes.Status409Conflict
            ),
            { Error: { } why } => AdminResults.Error(StatusCodes.Status400BadRequest, why),
            _ => Results.NoContent(),
        };
    }

    /// <summary>
    /// Habbo's pictures of these names that this hotel lacks, taken in as an asset job; 409
    /// while another asset job runs.
    /// </summary>
    private IResult FetchFromHabbo(CatalogPicturesRequest request, HttpContext http)
    {
        var names = (request.Names ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        if (names.Count == 0)
            return AdminResults.Error(StatusCodes.Status400BadRequest, "Name the pictures.");

        try
        {
            return Results.Ok(
                AssetJobResponse.From(
                    assets.StartCatalogPictures(AdminIdentity.Of(http).PlayerId, names)
                )
            );
        }
        catch (InvalidOperationException ex)
        {
            return AdminResults.Error(StatusCodes.Status409Conflict, ex.Message);
        }
    }

    private async Task<IResult> ListAsync(
        string? q,
        string? show,
        int? page,
        int? size,
        CancellationToken ct
    ) =>
        await images.ListAsync(q, show, page ?? 0, size ?? 60, ct).ConfigureAwait(false) is { } list
            ? Results.Ok(list)
            : AdminResults.Error(StatusCodes.Status400BadRequest, "Show is all or unused.");

    private async Task<IResult> FileAsync(string name, CancellationToken ct)
    {
        if (images.FilePathOf(name) is not { } path)
            return AdminResults.Error(StatusCodes.Status404NotFound, NO_IMAGE);

        var head = new byte[HEAD_BYTES];
        var stream = File.OpenRead(path);

        await using (stream.ConfigureAwait(false))
            await stream
                .ReadAtLeastAsync(head, HEAD_BYTES, throwOnEndOfStream: false, ct)
                .ConfigureAwait(false);

        return TypedResults.PhysicalFile(path, AdminCatalogImages.ContentTypeOf(head));
    }

    private async Task<IResult> UploadAsync(HttpContext http, CancellationToken ct)
    {
        if (!http.Request.HasFormContentType)
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                "Send the picture as a form."
            );

        var tooLarge =
            $"A picture is at most {AdminCatalogImages.UPLOAD_MAX_BYTES / (1024 * 1024)} MB.";

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
            return AdminResults.Error(StatusCodes.Status400BadRequest, tooLarge);
        }
        catch (InvalidDataException ex)
        {
            logger.LogWarning(ex, "A catalog picture upload's form could not be read");

            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                $"The form could not be read: {ex.Message}"
            );
        }

        if (form.Files.GetFile("file") is not { } file)
            return AdminResults.Error(StatusCodes.Status400BadRequest, "Choose a picture.");

        if (file.Length > AdminCatalogImages.UPLOAD_MAX_BYTES)
            return AdminResults.Error(StatusCodes.Status400BadRequest, tooLarge);

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
            return Results.Ok(
                await images
                    .UploadAsync(
                        form["name"].ToString(),
                        file.FileName,
                        data,
                        AdminIdentity.Of(http).PlayerId,
                        ct
                    )
                    .ConfigureAwait(false)
            );
        }
        catch (ArgumentException ex)
        {
            return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
        }
    }

    private async Task<IResult> DeleteAsync(string name, HttpContext http, CancellationToken ct)
    {
        return await images
            .DeleteAsync(name, AdminIdentity.Of(http).PlayerId, ct)
            .ConfigureAwait(false)
            ? Results.NoContent()
            : AdminResults.Error(StatusCodes.Status404NotFound, NO_IMAGE);
    }
}
