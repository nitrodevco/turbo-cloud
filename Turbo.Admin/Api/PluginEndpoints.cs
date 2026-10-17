using System;
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
using Turbo.Admin.Plugins;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Plugins;

namespace Turbo.Admin.Api;

/// <summary>
/// The server's plugins, for staff with <c>admin.plugins.view</c>. Reloading one, scanning the
/// folders again and turning one off or on need <c>admin.plugins.manage</c>; installing a package
/// needs <c>admin.plugins.install</c>, named on purpose, and <c>Turbo:Plugin:UploadEnabled</c>.
/// Every action, refused ones too, goes to the command log (<see cref="AdminPluginAudit"/>).
/// </summary>
internal sealed class PluginEndpoints(
    IGrainFactory grainFactory,
    IPluginAdministration plugins,
    AdminPluginAudit audit
)
{
    private const string NO_ACCESS = "You can't see the server's plugins.";
    private const string NO_MANAGE = "You can't change the server's plugins.";
    private const string NO_INSTALL = "You can't install plugins.";
    private const string UPLOAD_OFF =
        "Installing plugins from the panel is off. Turn on Turbo:Plugin:UploadEnabled to allow it.";

    /// <summary>Room for the form's boundaries and fields around the package.</summary>
    private const long FORM_OVERHEAD_BYTES = 64 * 1024;

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured.MapGroup("/plugins").AddEndpointFilter(RequireViewAsync);

        group.MapGet("/", ListAsync);
        group.MapPost("/rescan", RescanAsync);
        group.MapPost("/install", InstallAsync);
        group.MapPost("/{key}/reload", ReloadAsync);
        group.MapPost(
            "/{key}/enable",
            (string key, HttpContext http, CancellationToken ct) =>
                SetEnabledAsync(key, true, http, ct)
        );
        group.MapPost(
            "/{key}/disable",
            (string key, HttpContext http, CancellationToken ct) =>
                SetEnabledAsync(key, false, http, ct)
        );
    }

    private async Task<IResult> ListAsync(HttpContext http, CancellationToken ct)
    {
        var canManage = await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_MANAGE, ct)
            .ConfigureAwait(false);

        return Results.Ok(
            new PluginsResponse(
                [.. plugins.List().Select(PluginView.From)],
                canManage,
                canManage
                    && await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_INSTALL, ct)
                        .ConfigureAwait(false),
                plugins.UploadEnabled,
                plugins.MaxUploadBytes
            )
        );
    }

    private Task<IResult> RescanAsync(HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            "rescan",
            ct,
            async () => await plugins.RescanAsync(CancellationToken.None).ConfigureAwait(false)
        );

    private Task<IResult> ReloadAsync(string key, HttpContext http, CancellationToken ct) =>
        ManageAsync(
            http,
            $"reload {key}",
            ct,
            () => plugins.ReloadAsync(key, CancellationToken.None)
        );

    private Task<IResult> SetEnabledAsync(
        string key,
        bool enabled,
        HttpContext http,
        CancellationToken ct
    ) =>
        ManageAsync(
            http,
            $"{(enabled ? "enable" : "disable")} {key}",
            ct,
            () => plugins.SetEnabledAsync(key, enabled, CancellationToken.None)
        );

    private async Task<IResult> InstallAsync(HttpContext http, CancellationToken ct)
    {
        var player = AdminIdentity.Of(http).PlayerId;

        if (
            !await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_MANAGE, ct).ConfigureAwait(false)
            || !await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_INSTALL, ct)
                .ConfigureAwait(false)
        )
        {
            await audit
                .WriteAsync(player, "install", CommandOutcome.Refused, ct)
                .ConfigureAwait(false);

            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_INSTALL);
        }

        if (!plugins.UploadEnabled)
        {
            await audit
                .WriteAsync(player, "install", CommandOutcome.Refused, ct)
                .ConfigureAwait(false);

            return AdminResults.Error(StatusCodes.Status403Forbidden, UPLOAD_OFF);
        }

        if (!http.Request.HasFormContentType)
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                "Send the package as a form."
            );

        if (http.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
            limit.MaxRequestBodySize = plugins.MaxUploadBytes + FORM_OVERHEAD_BYTES;

        var tooLarge = $"The package is larger than {plugins.MaxUploadBytes / (1024 * 1024)} MB.";
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
        catch (InvalidDataException)
        {
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                "The form could not be read."
            );
        }

        if (form.Files.GetFile("file") is not { } file)
            return AdminResults.Error(StatusCodes.Status400BadRequest, "Choose a plugin package.");

        if (file.Length > plugins.MaxUploadBytes)
            return AdminResults.Error(StatusCodes.Status400BadRequest, tooLarge);

        PluginActionResult result;
        var stream = file.OpenReadStream();

        await using (stream.ConfigureAwait(false))
            // Not the request's token: a package half-copied into the plugin folder because the
            // browser went away would leave a broken plugin behind.
            result = await plugins
                .InstallAsync(stream, CancellationToken.None)
                .ConfigureAwait(false);

        await audit
            .WriteAsync(
                player,
                $"install {result.Plugin?.Key ?? file.FileName} {result.Plugin?.Version}".TrimEnd(),
                result.Ok ? CommandOutcome.Completed : CommandOutcome.Failed,
                CancellationToken.None
            )
            .ConfigureAwait(false);

        return Answer(result);
    }

    /// <summary>
    /// An action by someone who holds <c>admin.plugins.manage</c>, written to the command log
    /// with how it went. The action runs to its end even if the browser goes away.
    /// </summary>
    private async Task<IResult> ManageAsync(
        HttpContext http,
        string arguments,
        CancellationToken ct,
        Func<Task<PluginActionResult?>> action
    )
    {
        var player = AdminIdentity.Of(http).PlayerId;

        if (!await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_MANAGE, ct).ConfigureAwait(false))
        {
            await audit
                .WriteAsync(player, arguments, CommandOutcome.Refused, ct)
                .ConfigureAwait(false);

            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);
        }

        var result = await action().ConfigureAwait(false);

        await audit
            .WriteAsync(
                player,
                arguments,
                result is { Ok: true } ? CommandOutcome.Completed : CommandOutcome.Failed,
                CancellationToken.None
            )
            .ConfigureAwait(false);

        return result is null
            ? AdminResults.Error(StatusCodes.Status404NotFound, "There is no plugin with that key.")
            : Answer(result);
    }

    /// <summary>
    /// A plugin that did not do what was asked is still an answer the panel shows: the refusal or
    /// the load error is its message, with the plugin as it is now.
    /// </summary>
    private static IResult Answer(PluginActionResult result) =>
        result.Ok
            ? Results.Ok(PluginActionResponse.From(result))
            : Results.Json(
                PluginActionResponse.From(result),
                statusCode: StatusCodes.Status422UnprocessableEntity
            );

    private Task<bool> HoldsAsync(HttpContext http, string node, CancellationToken ct) =>
        grainFactory.HasPermissionAsync(AdminIdentity.Of(http).PlayerId, node, ct);

    private async ValueTask<object?> RequireViewAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var http = context.HttpContext;

        return await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_VIEW, http.RequestAborted)
            .ConfigureAwait(false)
            ? await next(context).ConfigureAwait(false)
            : AdminResults.Error(StatusCodes.Status403Forbidden, NO_ACCESS);
    }
}
