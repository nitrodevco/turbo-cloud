using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Turbo.Admin.Catalog;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The catalog's pictures (<see cref="AdminCatalogImages"/>): listed, searched and drawn for staff
/// with <c>admin.catalog.view</c>; uploaded and deleted by those who also hold <c>catalog.manage</c>.
/// </summary>
internal sealed class CatalogImageEndpoints(
    AdminCatalogImages images,
    ILogger<CatalogImageEndpoints> logger
)
{
    private const string NO_ACCESS = "You can't see the catalog.";
    private const string NO_MANAGE = "You can't change the catalog.";
    private const string NO_IMAGE = "There is no such picture.";

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
