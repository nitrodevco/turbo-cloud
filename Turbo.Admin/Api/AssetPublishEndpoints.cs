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
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;

namespace Turbo.Admin.Api;

/// <summary>
/// Where the hotel's asset bundles are published (<c>/api/assets/targets</c>): a folder on this
/// server, or an FTP, FTPS or SFTP server. Staff with <c>admin.gamedata.view</c> see the targets and
/// their history; adding, changing, testing and publishing need <c>gamedata.manage</c> as well.
/// </summary>
internal sealed class AssetPublishEndpoints(
    IGrainFactory grainFactory,
    IAssetPublishService publishing
)
{
    private const string NO_ACCESS = "You can't see the asset bundles.";
    private const string NO_MANAGE = "You can't change the asset bundles.";
    private const string NO_TARGET = "There is no such publish target.";
    private const string UNKNOWN_PROTOCOL = "The protocol is folder, ftp, ftps or sftp.";

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured.MapGroup("/assets/targets").AddEndpointFilter(RequireViewAsync);

        group.MapGet("/", ListAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id:int}", UpdateAsync);
        group.MapDelete("/{id:int}", DeleteAsync);
        group.MapPost("/{id:int}/test", TestAsync);
        group.MapPost("/{id:int}/publish", PublishAsync);
        group.MapPost("/{id:int}/forget-host-key", ForgetHostKeyAsync);
        group.MapGet("/{id:int}/history", HistoryAsync);
    }

    private async Task<IResult> ListAsync(CancellationToken ct)
    {
        var targets = await publishing.ListTargetsAsync(ct).ConfigureAwait(false);
        var names = await NamesAsync(targets.Select(x => x.LastPublish?.PlayerId ?? 0), ct)
            .ConfigureAwait(false);

        return Results.Ok(
            new AssetPublishTargetsResponse([
                .. targets.Select(x => AssetPublishTargetResponse.From(x, names)),
            ])
        );
    }

    private Task<IResult> CreateAsync(
        AssetPublishTargetInput input,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                if (EditOf(input) is not { } edit)
                    return AdminResults.Error(StatusCodes.Status400BadRequest, UNKNOWN_PROTOCOL);

                try
                {
                    var target = await publishing.CreateTargetAsync(edit, ct).ConfigureAwait(false);

                    return Results.Ok(await ResponseAsync(target, ct).ConfigureAwait(false));
                }
                catch (ArgumentException ex)
                {
                    return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
                }
            }
        );

    private Task<IResult> UpdateAsync(
        int id,
        AssetPublishTargetInput input,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                if (EditOf(input) is not { } edit)
                    return AdminResults.Error(StatusCodes.Status400BadRequest, UNKNOWN_PROTOCOL);

                try
                {
                    return
                        await publishing.UpdateTargetAsync(id, edit, ct).ConfigureAwait(false)
                            is { } target
                        ? Results.Ok(await ResponseAsync(target, ct).ConfigureAwait(false))
                        : AdminResults.Error(StatusCodes.Status404NotFound, NO_TARGET);
                }
                catch (ArgumentException ex)
                {
                    return AdminResults.Error(StatusCodes.Status400BadRequest, ex.Message);
                }
            }
        );

    private Task<IResult> DeleteAsync(int id, HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                try
                {
                    return await publishing.DeleteTargetAsync(id, ct).ConfigureAwait(false)
                        ? Results.NoContent()
                        : AdminResults.Error(StatusCodes.Status404NotFound, NO_TARGET);
                }
                catch (InvalidOperationException ex)
                {
                    return AdminResults.Error(StatusCodes.Status409Conflict, ex.Message);
                }
            }
        );

    private Task<IResult> TestAsync(int id, HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            ct,
            async () =>
                await publishing.TestAsync(id, ct).ConfigureAwait(false) is { } result
                    ? Results.Ok(new AssetPublishTestResponse(result.Ok, result.Message))
                    : AdminResults.Error(StatusCodes.Status404NotFound, NO_TARGET)
        );

    private Task<IResult> PublishAsync(
        int id,
        AssetPublishRequest? request,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            ct,
            async () =>
            {
                try
                {
                    return
                        await publishing
                            .PublishAsync(
                                id,
                                request?.DryRun ?? false,
                                request?.DeleteRemoved ?? false,
                                AdminIdentity.Of(http).PlayerId,
                                ct
                            )
                            .ConfigureAwait(false)
                            is { } job
                        ? Results.Ok(AssetPublishJobResponse.From(job))
                        : AdminResults.Error(StatusCodes.Status404NotFound, NO_TARGET);
                }
                catch (InvalidOperationException ex)
                {
                    return AdminResults.Error(StatusCodes.Status409Conflict, ex.Message);
                }
            }
        );

    private Task<IResult> ForgetHostKeyAsync(int id, HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            ct,
            async () =>
                await publishing.ForgetHostKeyAsync(id, ct).ConfigureAwait(false)
                    ? Results.NoContent()
                    : AdminResults.Error(StatusCodes.Status404NotFound, NO_TARGET)
        );

    private async Task<IResult> HistoryAsync(int id, CancellationToken ct)
    {
        if (await publishing.GetHistoryAsync(id, ct).ConfigureAwait(false) is not { } history)
            return AdminResults.Error(StatusCodes.Status404NotFound, NO_TARGET);

        var names = await NamesAsync(history.Select(x => x.PlayerId), ct).ConfigureAwait(false);

        return Results.Ok(
            new AssetPublishHistoryResponse([
                .. history.Select(x => AssetPublishHistoryItem.From(x, names)),
            ])
        );
    }

    /// <summary>The edit the input asks for; null when its protocol isn't one.</summary>
    private static AssetPublishTargetEdit? EditOf(AssetPublishTargetInput input) =>
        AssetPublishProtocols.TryParse(input.Protocol, out var protocol)
            ? new AssetPublishTargetEdit
            {
                Name = input.Name ?? "",
                Protocol = protocol,
                Host = input.Host ?? "",
                Port = input.Port,
                User = input.User ?? "",
                Password = input.Password,
                RemotePath = input.RemotePath ?? "",
                PublicUrl = input.PublicUrl ?? "",
                AllowSelfSigned = input.AllowSelfSigned,
            }
            : null;

    private async Task<AssetPublishTargetResponse> ResponseAsync(
        AssetPublishTargetSnapshot target,
        CancellationToken ct
    ) =>
        AssetPublishTargetResponse.From(
            target,
            await NamesAsync([target.LastPublish?.PlayerId ?? 0], ct).ConfigureAwait(false)
        );

    /// <summary>The names of the staff who started publishes, by id.</summary>
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
