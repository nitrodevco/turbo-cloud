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
using Turbo.Admin.Commands;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Plugins;

namespace Turbo.Admin.Api;

/// <summary>
/// The server's plugins, for staff with <c>admin.plugins.view</c>. Reloading one, scanning the
/// folders again and turning one off or on need <c>admin.plugins.manage</c>. Installing a package
/// needs <c>Turbo:Plugin:UploadEnabled</c> and <c>admin.plugins.manage</c>; a package with code
/// (<see cref="PluginPackage.HasCode"/>) needs <c>admin.plugins.install</c> as well, named on
/// purpose, and a data plugin bringing texts or variables <c>gamedata.manage</c> (or
/// <c>admin.plugins.install</c>). Rolling back to a kept version is installing it again. Linking a
/// GitHub repository and updating from it need <c>admin.plugins.install</c>: a release may be
/// code. Checking a link for a new release needs <c>admin.plugins.manage</c>.
/// Uninstalling one, its dry run and the SQL export of its tables need <c>admin.plugins.uninstall</c>
/// as well, and the plugin's key typed again. What each plugin contributes (the inspector) needs
/// only <c>admin.plugins.view</c>. Every action, refused ones too, goes to the command
/// log (<see cref="AdminActionLog"/>).
/// </summary>
internal sealed class PluginEndpoints(
    IGrainFactory grainFactory,
    IPluginAdministration plugins,
    IPluginSources sources,
    AdminActionLog audit
)
{
    private const string NO_ACCESS = "You can't see the server's plugins.";
    private const string NO_MANAGE = "You can't change the server's plugins.";
    private const string NO_INSTALL = "You can't install plugins.";
    private const string NO_UNINSTALL = "You can't uninstall plugins.";
    private const string NO_PLUGIN = "There is no plugin with that key.";
    private const string UPLOAD_OFF =
        "Installing plugins from the panel is off. Turn on Turbo:Plugin:UploadEnabled to allow it.";

    /// <summary>Room for the form's boundaries and fields around the package.</summary>
    private const long FORM_OVERHEAD_BYTES = 64 * 1024;

    public void Map(RouteGroupBuilder secured)
    {
        var group = secured
            .MapGroup("/plugins")
            .RequirePermission(PermissionNodes.Admin.PLUGINS_VIEW, NO_ACCESS)
            .ChecksInHandler(
                "Each change checks its node in the handler, so a refusal is written to the command log too."
            );

        group.MapGet("/", ListAsync);
        group.MapPost("/rescan", RescanAsync);
        group.MapPost("/install", InstallAsync);
        group.MapGet(
            "/job",
            () => sources.Job is { } job ? Results.Ok(PluginJobView.From(job)) : Results.NoContent()
        );
        group.MapPost("/job/cancel", CancelJobAsync);
        group.MapPost("/sources", AddSourceAsync);
        group.MapPost(
            "/sources/check",
            (HttpContext http, CancellationToken ct) => CheckAsync(null, http, ct)
        );
        group.MapPut("/sources/{id}", EditSourceAsync);
        group.MapDelete("/sources/{id}", RemoveSourceAsync);
        group.MapPost(
            "/sources/{id}/check",
            (string id, HttpContext http, CancellationToken ct) => CheckAsync(id, http, ct)
        );
        group.MapPost("/sources/{id}/update", UpdateAsync);
        group.MapPost("/{key}/rollback", RollbackAsync);
        group.MapGet("/{key}/uninstall", PlanUninstallAsync);
        group.MapPost("/{key}/uninstall", UninstallAsync);
        group.MapGet("/{key}/export", ExportAsync);
        group.MapGet(
            "/{key}/contributions",
            (string key) =>
                plugins.ContributionsOf(key) is { } contributions
                    ? Results.Ok(PluginContributionsView.From(contributions))
                    : AdminResults.Error(StatusCodes.Status404NotFound, NO_PLUGIN)
        );
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
                [.. plugins.List().Select(p => PluginView.From(p, plugins.VersionsOf(p.Key)))],
                canManage,
                canManage
                    && await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_INSTALL, ct)
                        .ConfigureAwait(false),
                plugins.UploadEnabled,
                plugins.MaxUploadBytes,
                [.. sources.List().Select(PluginSourceView.From)],
                sources.Job is { } job ? PluginJobView.From(job) : null,
                [.. sources.AllowedOwners],
                sources.CheckMinutes,
                canManage
                    && await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_UNINSTALL, ct)
                        .ConfigureAwait(false)
            )
        );
    }

    private async Task<IResult> PlanUninstallAsync(
        string key,
        HttpContext http,
        CancellationToken ct
    )
    {
        if (
            await RefuseUninstallAsync(http, $"uninstall plan {key}", ct).ConfigureAwait(false) is
            { } refused
        )
            return refused;

        return await plugins.PlanUninstallAsync(key, ct).ConfigureAwait(false) is { } plan
            ? Results.Ok(PluginUninstallPlanView.From(plan))
            : AdminResults.Error(StatusCodes.Status404NotFound, NO_PLUGIN);
    }

    private async Task<IResult> UninstallAsync(
        string key,
        PluginUninstallRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        var arguments = $"uninstall {key} {request.Mode}".TrimEnd();

        if (await RefuseUninstallAsync(http, arguments, ct).ConfigureAwait(false) is { } refused)
            return refused;

        if (request.ParsedMode is not { } mode)
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                $"Choose {PluginUninstallRequest.KEEP_DATA} or {PluginUninstallRequest.DELETE_DATA}."
            );

        if (!string.Equals(request.Confirm?.Trim(), key, StringComparison.Ordinal))
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                $"Type the plugin's key, {key}, to uninstall it."
            );

        // Not the request's token: a plugin half taken away would be neither installed nor gone.
        var result = await plugins
            .UninstallAsync(key, mode, AdminIdentity.Of(http).PlayerId, CancellationToken.None)
            .ConfigureAwait(false);

        await audit
            .WriteAsync(
                AdminActionLog.PLUGIN,
                AdminIdentity.Of(http).PlayerId,
                arguments,
                result is { Ok: true } ? CommandOutcome.Completed : CommandOutcome.Failed,
                CancellationToken.None
            )
            .ConfigureAwait(false);

        return result is null
            ? AdminResults.Error(StatusCodes.Status404NotFound, NO_PLUGIN)
            : Answer(result);
    }

    private async Task<IResult> ExportAsync(string key, HttpContext http, CancellationToken ct)
    {
        if (
            await RefuseUninstallAsync(http, $"export {key}", ct).ConfigureAwait(false) is
            { } refused
        )
            return refused;

        if (!plugins.List().Any(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)))
            return AdminResults.Error(StatusCodes.Status404NotFound, NO_PLUGIN);

        // Written whole before it is sent: a failure is an error, not a cut-off file.
        var sql = new MemoryStream();

        await plugins.ExportDataAsync(key, sql, ct).ConfigureAwait(false);

        await audit
            .WriteAsync(
                AdminActionLog.PLUGIN,
                AdminIdentity.Of(http).PlayerId,
                $"export {key}",
                CommandOutcome.Completed,
                CancellationToken.None
            )
            .ConfigureAwait(false);

        sql.Position = 0;

        return Results.File(sql, "application/sql", $"{key}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.sql");
    }

    /// <summary>
    /// A refusal, written to the command log, unless the staff member may uninstall plugins:
    /// <c>admin.plugins.manage</c> and <c>admin.plugins.uninstall</c>.
    /// </summary>
    private async Task<IResult?> RefuseUninstallAsync(
        HttpContext http,
        string arguments,
        CancellationToken ct
    )
    {
        if (
            await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_MANAGE, ct).ConfigureAwait(false)
            && await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_UNINSTALL, ct)
                .ConfigureAwait(false)
        )
            return null;

        await audit
            .WriteAsync(
                AdminActionLog.PLUGIN,
                AdminIdentity.Of(http).PlayerId,
                arguments,
                CommandOutcome.Refused,
                ct
            )
            .ConfigureAwait(false);

        return AdminResults.Error(StatusCodes.Status403Forbidden, NO_UNINSTALL);
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
            await RefuseInstallAsync(http, "install", ct, code: false).ConfigureAwait(false) is
            { } refusedUpload
        )
            return refusedUpload;

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

        var refuse = await RefusalAsync(http, ct).ConfigureAwait(false);
        PluginPackage? refused = null;
        PluginActionResult result;
        var stream = file.OpenReadStream();

        await using (stream.ConfigureAwait(false))
            // Not the request's token: a package half-copied into the plugin folder because the
            // browser went away would leave a broken plugin behind.
            result = await plugins
                .InstallAsync(
                    stream,
                    package =>
                    {
                        var why = refuse(package);

                        refused = why is null ? null : package;

                        return why;
                    },
                    CancellationToken.None
                )
                .ConfigureAwait(false);

        await audit
            .WriteAsync(
                AdminActionLog.PLUGIN,
                player,
                refused is not null
                    ? $"install {refused.Key} {refused.Version}"
                    : $"install {result.Plugin?.Key ?? file.FileName} {result.Plugin?.Version}".TrimEnd(),
                refused is not null ? CommandOutcome.Refused
                    : result.Ok ? CommandOutcome.Completed
                    : CommandOutcome.Failed,
                CancellationToken.None
            )
            .ConfigureAwait(false);

        return refused is not null
            ? AdminResults.Error(StatusCodes.Status403Forbidden, result.Message)
            : Answer(result);
    }

    /// <summary>
    /// What the staff member may not install, as the reason to refuse a package or null: any
    /// package with <c>admin.plugins.install</c>; without it only a data plugin that replaces no
    /// plugin with code, and one bringing texts or variables only with <c>gamedata.manage</c>.
    /// </summary>
    private async Task<Func<PluginPackage, string?>> RefusalAsync(
        HttpContext http,
        CancellationToken ct
    )
    {
        if (await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_INSTALL, ct).ConfigureAwait(false))
            return _ => null;

        var gamedata = await HoldsAsync(http, PermissionNodes.Gamedata.MANAGE, ct)
            .ConfigureAwait(false);

        return package =>
            package.CodeFile is { } file
                ? $"{package.Name} has code ({file}): installing it needs {PermissionNodes.Admin.PLUGINS_INSTALL}. A data plugin (no AssemblyFile; JSON, text and images only) needs only {PermissionNodes.Admin.PLUGINS_MANAGE}."
            : package.ReplacesCode
                ? $"{package.Name} would replace the installed plugin {package.Key}, which has code: that needs {PermissionNodes.Admin.PLUGINS_INSTALL}."
            : package.Gamedata && !gamedata
                ? $"{package.Name} brings texts or variables (its gamedata folder): installing it needs {PermissionNodes.Gamedata.MANAGE} too."
            : null;
    }

    private async Task<IResult> AddSourceAsync(
        PluginSourceRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        var repository = request.Repository?.Trim() ?? "";

        if (
            await RefuseInstallAsync(http, $"source add {repository}", ct).ConfigureAwait(false) is
            { } refused
        )
            return refused;

        var result = sources.Add(request.ToInput(), AdminIdentity.Of(http).PlayerId.Value);

        return await AnswerAsync(
                http,
                $"source add {result.Source?.Repository ?? repository}",
                result
            )
            .ConfigureAwait(false);
    }

    private async Task<IResult> EditSourceAsync(
        string id,
        PluginSourceRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        var name = NameOf(id);

        if (
            await RefuseInstallAsync(http, $"source edit {name}", ct).ConfigureAwait(false) is
            { } refused
        )
            return refused;

        return await AnswerAsync(http, $"source edit {name}", sources.Edit(id, request.ToInput()))
            .ConfigureAwait(false);
    }

    private async Task<IResult> RemoveSourceAsync(string id, HttpContext http, CancellationToken ct)
    {
        var name = NameOf(id);

        if (
            await RefuseManageAsync(http, $"source remove {name}", ct).ConfigureAwait(false) is
            { } refused
        )
            return refused;

        return await AnswerAsync(http, $"source remove {name}", sources.Remove(id))
            .ConfigureAwait(false);
    }

    private async Task<IResult> CheckAsync(string? id, HttpContext http, CancellationToken ct)
    {
        var name = id is null ? "all" : NameOf(id);

        if (await RefuseManageAsync(http, $"check {name}", ct).ConfigureAwait(false) is { } refused)
            return refused;

        return await AnswerAsync(
                http,
                $"check {name}",
                sources.Check(id, AdminIdentity.Of(http).PlayerId.Value)
            )
            .ConfigureAwait(false);
    }

    private async Task<IResult> UpdateAsync(string id, HttpContext http, CancellationToken ct)
    {
        var name = NameOf(id);

        if (
            await RefuseInstallAsync(http, $"update {name}", ct).ConfigureAwait(false) is
            { } refused
        )
            return refused;

        return await AnswerAsync(
                http,
                $"update {name}",
                sources.Update(id, AdminIdentity.Of(http).PlayerId.Value)
            )
            .ConfigureAwait(false);
    }

    private async Task<IResult> CancelJobAsync(HttpContext http, CancellationToken ct)
    {
        if (await RefuseManageAsync(http, "stop job", ct).ConfigureAwait(false) is { } refused)
            return refused;

        var stopped = sources.CancelJob();

        await audit
            .WriteAsync(
                AdminActionLog.PLUGIN,
                AdminIdentity.Of(http).PlayerId,
                "stop job",
                stopped ? CommandOutcome.Completed : CommandOutcome.Failed,
                CancellationToken.None
            )
            .ConfigureAwait(false);

        return stopped
            ? Results.NoContent()
            : AdminResults.Error(StatusCodes.Status409Conflict, "No plugin job is running.");
    }

    private async Task<IResult> RollbackAsync(
        string key,
        PluginRollbackRequest request,
        HttpContext http,
        CancellationToken ct
    )
    {
        var version = request.Version?.Trim() ?? "";
        var arguments = $"rollback {key} {version}".TrimEnd();

        if (
            await RefuseInstallAsync(http, arguments, ct, code: false).ConfigureAwait(false) is
            { } refused
        )
            return refused;

        if (version.Length == 0)
            return AdminResults.Error(
                StatusCodes.Status400BadRequest,
                "Choose the version to roll back to."
            );

        var refuse = await RefusalAsync(http, ct).ConfigureAwait(false);
        var refusedPackage = false;

        // Not the request's token: a plugin folder half-replaced would be a broken plugin.
        var result = await plugins
            .RollbackAsync(
                key,
                version,
                package =>
                {
                    var why = refuse(package);

                    refusedPackage = why is not null;

                    return why;
                },
                CancellationToken.None
            )
            .ConfigureAwait(false);

        await audit
            .WriteAsync(
                AdminActionLog.PLUGIN,
                AdminIdentity.Of(http).PlayerId,
                arguments,
                refusedPackage ? CommandOutcome.Refused
                    : result is { Ok: true } ? CommandOutcome.Completed
                    : CommandOutcome.Failed,
                CancellationToken.None
            )
            .ConfigureAwait(false);

        return result is null
                ? AdminResults.Error(
                    StatusCodes.Status404NotFound,
                    "That plugin has no such version kept."
                )
            : refusedPackage ? AdminResults.Error(StatusCodes.Status403Forbidden, result.Message)
            : Answer(result);
    }

    /// <summary>
    /// A refusal, written to the command log, unless the staff member may put server code in
    /// place: <c>admin.plugins.manage</c> and <c>admin.plugins.install</c>, on a server that allows
    /// installing from the panel. Not <paramref name="code"/>: only <c>admin.plugins.manage</c>,
    /// and the package is checked as it is unpacked (<see cref="RefusalAsync"/>).
    /// </summary>
    private async Task<IResult?> RefuseInstallAsync(
        HttpContext http,
        string arguments,
        CancellationToken ct,
        bool code = true
    )
    {
        if (
            !await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_MANAGE, ct).ConfigureAwait(false)
            || code
                && !await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_INSTALL, ct)
                    .ConfigureAwait(false)
        )
        {
            await audit
                .WriteAsync(
                    AdminActionLog.PLUGIN,
                    AdminIdentity.Of(http).PlayerId,
                    arguments,
                    CommandOutcome.Refused,
                    ct
                )
                .ConfigureAwait(false);

            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_INSTALL);
        }

        if (!plugins.UploadEnabled)
        {
            await audit
                .WriteAsync(
                    AdminActionLog.PLUGIN,
                    AdminIdentity.Of(http).PlayerId,
                    arguments,
                    CommandOutcome.Refused,
                    ct
                )
                .ConfigureAwait(false);

            return AdminResults.Error(StatusCodes.Status403Forbidden, UPLOAD_OFF);
        }

        return null;
    }

    private async Task<IResult?> RefuseManageAsync(
        HttpContext http,
        string arguments,
        CancellationToken ct
    )
    {
        if (await HoldsAsync(http, PermissionNodes.Admin.PLUGINS_MANAGE, ct).ConfigureAwait(false))
            return null;

        await audit
            .WriteAsync(
                AdminActionLog.PLUGIN,
                AdminIdentity.Of(http).PlayerId,
                arguments,
                CommandOutcome.Refused,
                ct
            )
            .ConfigureAwait(false);

        return AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);
    }

    /// <summary>
    /// A link action's answer, written to the command log: a job it started is <c>Completed</c>
    /// here (how the job ends is in its log), a refusal <c>Failed</c>; 404 for no such link.
    /// </summary>
    private async Task<IResult> AnswerAsync(
        HttpContext http,
        string arguments,
        PluginSourceResult? result
    )
    {
        await audit
            .WriteAsync(
                AdminActionLog.PLUGIN,
                AdminIdentity.Of(http).PlayerId,
                arguments,
                result is { Ok: true } ? CommandOutcome.Completed : CommandOutcome.Failed,
                CancellationToken.None
            )
            .ConfigureAwait(false);

        if (result is null)
            return AdminResults.Error(
                StatusCodes.Status404NotFound,
                "There is no such linked repository."
            );

        return result.Ok
            ? Results.Ok(PluginSourceActionResponse.From(result))
            : Results.Json(
                PluginSourceActionResponse.From(result),
                statusCode: StatusCodes.Status422UnprocessableEntity
            );
    }

    /// <summary>The link's repository for the command log, or its id when there is none.</summary>
    private string NameOf(string id) =>
        sources.List().FirstOrDefault(x => x.Id == id)?.Repository ?? id;

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
                .WriteAsync(AdminActionLog.PLUGIN, player, arguments, CommandOutcome.Refused, ct)
                .ConfigureAwait(false);

            return AdminResults.Error(StatusCodes.Status403Forbidden, NO_MANAGE);
        }

        var result = await action().ConfigureAwait(false);

        await audit
            .WriteAsync(
                AdminActionLog.PLUGIN,
                player,
                arguments,
                result is { Ok: true } ? CommandOutcome.Completed : CommandOutcome.Failed,
                CancellationToken.None
            )
            .ConfigureAwait(false);

        return result is null
            ? AdminResults.Error(StatusCodes.Status404NotFound, NO_PLUGIN)
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
        AdminIdentity.Of(http).HoldsAsync(grainFactory, node, ct);
}
