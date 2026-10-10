using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Enums;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// The hotel's asset bundles (<c>docs/asset-bundles.md</c>): their counts, list and files, the
/// checks against the hotel, and the sync from Habbo - for staff with <c>admin.gamedata.view</c>.
/// Uploading, deleting and syncing need <c>gamedata.manage</c> as well. Publish targets
/// (<c>/assets/targets</c>) are mapped on their own.
/// </summary>
internal sealed class AssetEndpoints(
    IGrainFactory grainFactory,
    IAssetBundleService bundles,
    IAssetSyncService sync,
    IAssetJobs jobs,
    ILogger<AssetEndpoints> logger
)
{
    private const string NO_ACCESS = "You can't see the asset bundles.";
    private const string NO_MANAGE = "You can't change the asset bundles.";
    private const string NO_BUNDLE = "There is no such bundle.";
    private const string BAD_KIND = "The kind is furniture, effect, pet or figure.";
    private const string BAD_STATUS = "The status is all, ok, failed or unused.";
    private const string NITRO_CONTENT_TYPE = "application/octet-stream";

    // Room for the upload form's other fields and boundaries, beside the file itself.
    private const long FORM_OVERHEAD_BYTES = 64 * 1024;

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured.MapGroup("/assets").AddEndpointFilter(RequireViewAsync);

        group.MapGet("/", OverviewAsync);
        group.MapGet(
            "/bundles",
            (string? kind, string? q, string? status, int? page, CancellationToken ct) =>
                ListAsync(kind, q, status, page, ct)
        );
        group.MapGet("/bundles/{kind}/{name}", DetailAsync);
        group.MapGet("/bundles/{kind}/{name}/file", FileAsync);
        group.MapPost("/bundles", UploadAsync);
        group.MapDelete("/bundles/{kind}/{name}", DeleteAsync);
        group.MapPost("/sync", SyncAsync);
        group.MapGet(
            "/job",
            () =>
                jobs.Current is { } job
                    ? Results.Ok(AssetJobResponse.From(job))
                    : Results.NoContent()
        );
        group.MapPost("/job/cancel", CancelAsync);
        group.MapGet("/checks", ChecksAsync);
    }

    private async Task<IResult> OverviewAsync(HttpContext http, CancellationToken ct) =>
        Results.Ok(
            AssetOverviewResponse.From(
                await bundles.GetOverviewAsync(ct).ConfigureAwait(false),
                await CanManageAsync(http, ct).ConfigureAwait(false),
                jobs.Current
            )
        );

    private async Task<IResult> ListAsync(
        string? kind,
        string? q,
        string? status,
        int? page,
        CancellationToken ct
    )
    {
        AssetBundleKind? only = null;

        if (!string.IsNullOrWhiteSpace(kind))
        {
            if (!AssetContractNames.TryParseKind(kind, out var parsed))
                return AdminResults.Error(StatusCodes.Status400BadRequest, BAD_KIND);

            only = parsed;
        }

        if (!AssetContractNames.TryParseStatus(status, out var filter))
            return AdminResults.Error(StatusCodes.Status400BadRequest, BAD_STATUS);

        return Results.Ok(
            AssetBundlesResponse.From(
                await bundles.ListAsync(only, q, filter, page ?? 0, ct).ConfigureAwait(false)
            )
        );
    }

    private async Task<IResult> DetailAsync(string kind, string name, CancellationToken ct)
    {
        if (!AssetContractNames.TryParseKind(kind, out var parsed))
            return AdminResults.Error(StatusCodes.Status400BadRequest, BAD_KIND);

        return await bundles.GetAsync(parsed, name, ct).ConfigureAwait(false) is { } detail
            ? Results.Ok(AssetBundleDetailResponse.From(detail))
            : AdminResults.Error(StatusCodes.Status404NotFound, NO_BUNDLE);
    }

    private async Task<IResult> FileAsync(string kind, string name, CancellationToken ct)
    {
        if (!AssetContractNames.TryParseKind(kind, out var parsed))
            return AdminResults.Error(StatusCodes.Status400BadRequest, BAD_KIND);

        return await bundles.GetFilePathAsync(parsed, name, ct).ConfigureAwait(false) is { } path
            ? TypedResults.PhysicalFile(path, NITRO_CONTENT_TYPE, Path.GetFileName(path))
            : AdminResults.Error(StatusCodes.Status404NotFound, NO_BUNDLE);
    }

    private Task<IResult> UploadAsync(HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                if (!http.Request.HasFormContentType)
                    return AdminResults.Error(
                        StatusCodes.Status400BadRequest,
                        "Send the file as a form."
                    );

                var tooLarge = string.Create(
                    CultureInfo.InvariantCulture,
                    $"The file is larger than {bundles.UploadMaxBytes / (1024 * 1024)} MB."
                );

                if (
                    http.Features.Get<IHttpMaxRequestBodySizeFeature>() is
                    { IsReadOnly: false } limit
                )
                    limit.MaxRequestBodySize = bundles.UploadMaxBytes + FORM_OVERHEAD_BYTES;

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
                    logger.LogWarning(ex, "An asset upload's form could not be read");

                    return AdminResults.Error(
                        StatusCodes.Status400BadRequest,
                        $"The form could not be read: {ex.Message}"
                    );
                }

                if (form.Files.GetFile("file") is not { } file)
                    return AdminResults.Error(
                        StatusCodes.Status400BadRequest,
                        "Choose a .swf, .hab or .nitro file."
                    );

                if (!AssetContractNames.TryParseKind(form["kind"], out var kind))
                    return AdminResults.Error(StatusCodes.Status400BadRequest, BAD_KIND);

                if (file.Length > bundles.UploadMaxBytes)
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
                    var bundle = await bundles
                        .UploadAsync(
                            kind,
                            form["name"].FirstOrDefault(),
                            file.FileName,
                            data,
                            AdminIdentity.Of(http).PlayerId,
                            ct
                        )
                        .ConfigureAwait(false);

                    return Results.Ok(AssetBundleResponse.From(bundle));
                }
                catch (ArgumentException ex)
                {
                    logger.LogWarning(
                        "Player {PlayerId} uploaded {FileName} as an asset bundle, refused: {Reason}",
                        AdminIdentity.Of(http).PlayerId,
                        file.FileName,
                        ex.Message
                    );

                    return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
                }
            }
        );

    private Task<IResult> DeleteAsync(
        string kind,
        string name,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                if (!AssetContractNames.TryParseKind(kind, out var parsed))
                    return AdminResults.Error(StatusCodes.Status400BadRequest, BAD_KIND);

                return await bundles
                    .DeleteAsync(parsed, name, AdminIdentity.Of(http).PlayerId, ct)
                    .ConfigureAwait(false)
                    ? Results.NoContent()
                    : AdminResults.Error(StatusCodes.Status404NotFound, NO_BUNDLE);
            }
        );

    private Task<IResult> SyncAsync(HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            ct,
            () =>
            {
                try
                {
                    return Task.FromResult(
                        Results.Ok(
                            AssetJobResponse.From(sync.Start(AdminIdentity.Of(http).PlayerId))
                        )
                    );
                }
                catch (InvalidOperationException ex)
                {
                    return Task.FromResult(
                        AdminResults.Error(StatusCodes.Status409Conflict, ex.Message)
                    );
                }
            }
        );

    private Task<IResult> CancelAsync(HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            ct,
            () =>
                Task.FromResult(
                    jobs.Cancel()
                        ? Results.NoContent()
                        : AdminResults.Error(
                            StatusCodes.Status409Conflict,
                            "No asset job is running."
                        )
                )
        );

    private async Task<IResult> ChecksAsync(CancellationToken ct) =>
        Results.Ok(
            new AssetChecksResponse(
                [
                    .. (await bundles.GetChecksAsync(ct).ConfigureAwait(false)).Select(
                        AssetCheckResponse.From
                    ),
                ]
            )
        );

    /// <summary>A change by someone who holds <c>gamedata.manage</c>.</summary>
    private async Task<IResult> ManageAsync(
        HttpContext http,
        CancellationToken ct,
        Func<Task<IResult>> change
    ) =>
        await CanManageAsync(http, ct).ConfigureAwait(false)
            ? await change().ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);

    private Task<bool> CanManageAsync(HttpContext http, CancellationToken ct) =>
        grainFactory.HasPermissionAsync(
            AdminIdentity.Of(http).PlayerId,
            PermissionNodes.Gamedata.MANAGE,
            ct
        );

    private async ValueTask<object?> RequireViewAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var http = context.HttpContext;

        return await grainFactory
            .HasPermissionAsync(
                AdminIdentity.Of(http).PlayerId,
                PermissionNodes.Admin.GAMEDATA_VIEW,
                http.RequestAborted
            )
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }
}
