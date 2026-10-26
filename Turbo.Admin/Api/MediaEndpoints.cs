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
using Microsoft.Extensions.Logging;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Commands;
using Turbo.Admin.Media;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The media library (<see cref="MediaLibrary"/>): searched, drawn and its images' uses listed for
/// staff with <c>admin.gamedata.view</c>; uploaded to, renamed, tagged and deleted from by those
/// who also hold <c>gamedata.manage</c>, each change in the command log. An image is named by its
/// id in the query (<c>?id=library:web_promo/summer.png</c>), as ids hold slashes.
/// </summary>
internal sealed class MediaEndpoints(
    MediaLibrary media,
    AdminActionLog audit,
    ILogger<MediaEndpoints> logger
)
{
    private const string NO_ACCESS = "You can't see the media library.";
    private const string NO_MANAGE = "You can't change the media library.";
    private const string NO_IMAGE = "There is no such image.";

    // A few files at once, each at most an image's largest, and the form around them.
    private const int UPLOAD_MAX_FILES = 20;
    private const long FORM_OVERHEAD_BYTES = 64 * 1024;

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/media")
            .RequirePermission(PermissionNodes.Admin.GAMEDATA_VIEW, NO_ACCESS)
            .RequirePermissionToChange(PermissionNodes.Gamedata.MANAGE, NO_MANAGE);

        group.MapGet("/", SearchAsync);
        group.MapGet("/item", ItemAsync);
        group.MapGet("/file", FileAsync);
        group.MapPost("/", UploadAsync);
        group.MapPatch("/item", EditAsync);
        group.MapDelete("/item", DeleteAsync);
    }

    private Task<MediaSearchResponse> SearchAsync(
        string? q,
        string? kind,
        string? source,
        string? shape,
        string? used,
        int? minWidth,
        int? maxWidth,
        int? minHeight,
        int? maxHeight,
        string? sort,
        bool? desc,
        int? page,
        int? size,
        CancellationToken ct
    ) =>
        media.SearchAsync(
            new MediaQuery(
                q,
                kind,
                source,
                shape,
                used,
                minWidth,
                maxWidth,
                minHeight,
                maxHeight,
                sort,
                desc ?? false,
                page ?? 0,
                size ?? 60
            ),
            ct
        );

    private async Task<IResult> ItemAsync(string? id, CancellationToken ct) =>
        await media.GetAsync(id ?? "", ct).ConfigureAwait(false) is { } image
            ? Results.Ok(image)
            : AdminResults.Error(StatusCodes.Status404NotFound, NO_IMAGE);

    private async Task<IResult> FileAsync(string? id, CancellationToken ct) =>
        await media.ReadAsync(id ?? "", ct).ConfigureAwait(false) is { } file
            ? Results.File(file.Data, file.ContentType)
            : AdminResults.Error(StatusCodes.Status404NotFound, NO_IMAGE);

    private async Task<IResult> UploadAsync(HttpContext http, CancellationToken ct)
    {
        if (!http.Request.HasFormContentType)
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                "Send the images as a form."
            );

        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize =
                (long)MediaLibrary.UPLOAD_MAX_BYTES * UPLOAD_MAX_FILES + FORM_OVERHEAD_BYTES;

        IFormCollection form;

        try
        {
            form = await http.Request.ReadFormAsync(ct).ConfigureAwait(false);
        }
        catch (BadHttpRequestException ex)
            when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                $"Send at most {UPLOAD_MAX_FILES} images of {MediaLibrary.UPLOAD_MAX_BYTES / (1024 * 1024)} MB at once."
            );
        }
        catch (InvalidDataException ex)
        {
            logger.LogWarning(ex, "A media library upload's form could not be read");

            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                $"The form could not be read: {ex.Message}"
            );
        }

        if (form.Files.Count == 0)
            return AdminResults.Error(StatusCodes.Status400BadRequest, "Choose an image.");

        if (form.Files.Count > UPLOAD_MAX_FILES)
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                $"Send at most {UPLOAD_MAX_FILES} images at once."
            );

        var files = new List<(string, byte[])>();

        foreach (var file in form.Files)
        {
            if (file.Length > MediaLibrary.UPLOAD_MAX_BYTES)
            {
                files.Add((file.FileName, new byte[MediaLibrary.UPLOAD_MAX_BYTES + 1]));
                continue;
            }

            using var memory = new MemoryStream((int)file.Length);
            var stream = file.OpenReadStream();
            await using var streamScope = stream.ConfigureAwait(false);

            await stream.CopyToAsync(memory, ct).ConfigureAwait(false);
            files.Add((file.FileName, memory.ToArray()));
        }

        var player = AdminIdentity.Of(http).PlayerId;
        var result = await media
            .UploadAsync(files, form["folder"].ToString(), form["kind"].ToString(), player, ct)
            .ConfigureAwait(false);

        await audit
            .WriteAsync(
                AdminActionLog.MEDIA,
                player,
                $"upload {string.Join(' ', result.Items.Select(x => x.Id))}".Trim(),
                result.Items.Count > 0 ? CommandOutcome.Completed : CommandOutcome.Refused,
                ct
            )
            .ConfigureAwait(false);

        return Results.Ok(result);
    }

    private async Task<IResult> EditAsync(
        string? id,
        MediaEditRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        var player = AdminIdentity.Of(http).PlayerId;
        var outcome = await media.EditAsync(id ?? "", request, player, ct).ConfigureAwait(false);

        await LogAsync($"edit {id} {request.Name}".Trim(), outcome, http, ct).ConfigureAwait(false);

        return Answer(outcome);
    }

    private async Task<IResult> DeleteAsync(string? id, HttpContext http, CancellationToken ct)
    {
        var outcome = await media
            .DeleteAsync(id ?? "", AdminIdentity.Of(http).PlayerId, ct)
            .ConfigureAwait(false);

        await LogAsync($"delete {id}", outcome, http, ct).ConfigureAwait(false);

        return outcome is { Found: true, Error: null } ? Results.NoContent() : Answer(outcome);
    }

    private Task LogAsync(
        string arguments,
        MediaEditOutcome outcome,
        HttpContext http,
        CancellationToken ct
    ) =>
        outcome.Found
            ? audit.WriteAsync(
                AdminActionLog.MEDIA,
                AdminIdentity.Of(http).PlayerId,
                arguments,
                outcome.Error is null ? CommandOutcome.Completed : CommandOutcome.Refused,
                ct
            )
            : Task.CompletedTask;

    private static IResult Answer(MediaEditOutcome outcome) =>
        outcome switch
        {
            { Found: false } => AdminResults.Error(StatusCodes.Status404NotFound, NO_IMAGE),
            { UsedOn: { } uses, Error: { } error } => Results.Json(
                new MediaInUseResponse(error, uses),
                statusCode: StatusCodes.Status409Conflict
            ),
            { Error: { } why } => AdminResults.Error(StatusCodes.Status400BadRequest, why),
            _ => Results.Ok(outcome.Image),
        };
}
