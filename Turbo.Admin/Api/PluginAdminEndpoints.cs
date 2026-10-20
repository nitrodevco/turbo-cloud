using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Admin.Commands;
using Turbo.Admin.Configuration;
using Turbo.Contracts.Plugins.Admin;
using Turbo.Primitives.Commands;
using Turbo.Primitives.Orleans;

namespace Turbo.Admin.Api;

/// <summary>
/// What plugins add to the panel (<see cref="IPluginAdminRoutes"/>): the pages of the live ones,
/// each shown to staff holding its node, and their endpoints under
/// <c>/api/plugins/&lt;key&gt;/api/</c>. The host stands between the panel and the plugin: the
/// session is checked, then the plugin's state (404 unknown, 410 off or removed, 503 reloading),
/// the endpoint's node (403), the body's size (413) and the time it takes (504); an exception is
/// an error answer (500), never the host's. Every call but a GET goes to the command log as
/// <c>plugin call &lt;key&gt; &lt;method&gt; &lt;path&gt; &lt;status&gt;</c>, refusals included.
/// </summary>
internal sealed class PluginAdminEndpoints(
    IGrainFactory grainFactory,
    IPluginAdminRoutes routes,
    AdminActionLog audit,
    IOptions<AdminConfig> config,
    ILogger<PluginAdminEndpoints> logger
)
{
    private static readonly string[] METHODS = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    private readonly AdminConfig _config = config.Value;

    public void Map(RouteGroupBuilder secured)
    {
        secured
            .MapGet("/plugins/pages", PagesAsync)
            .PanelOnly("Lists only the plugin pages whose node the caller holds.");
        secured
            .MapMethods("/plugins/{key}/api/{**path}", METHODS, CallAsync)
            .PanelOnly("Any staff member may call a plugin route whose node they hold.")
            .ChecksInHandler("Each plugin route declares its own node, checked when it is called.");
    }

    /// <summary>Each live plugin's pages its viewer may see, with the plugin's nodes they hold (for its actions).</summary>
    private async Task<IResult> PagesAsync(HttpContext http, CancellationToken ct)
    {
        var plugins = new List<PluginAdminPages>();

        foreach (var plugin in routes.Pages())
        {
            var held = new List<string>();

            foreach (var node in plugin.Nodes)
                if (await HoldsAsync(http, node, ct).ConfigureAwait(false))
                    held.Add(node);

            var pages = plugin.Pages.Where(x => held.Contains(x.Node)).ToList();

            if (pages.Count > 0)
                plugins.Add(plugin with { Pages = pages, Nodes = held });
        }

        return Results.Ok(plugins);
    }

    private async Task<IResult> CallAsync(
        string key,
        string? path,
        HttpContext http,
        CancellationToken ct
    )
    {
        var method = http.Request.Method.ToUpperInvariant();

        path ??= string.Empty;

        var result = await AnswerAsync(key, method, path, http, ct).ConfigureAwait(false);

        if (method != "GET")
            await audit
                .WriteAsync(
                    AdminActionLog.PLUGIN,
                    AdminIdentity.Of(http).PlayerId,
                    $"call {key} {method} /{path} {result.Status}",
                    result.Status switch
                    {
                        < 400 => CommandOutcome.Completed,
                        403 => CommandOutcome.Refused,
                        >= 500 => CommandOutcome.Error,
                        _ => CommandOutcome.Failed,
                    },
                    CancellationToken.None
                )
                .ConfigureAwait(false);

        return result.Value is { } value ? value : AdminResults.Error(result.Status, result.Error!);
    }

    private async Task<Answer> AnswerAsync(
        string key,
        string method,
        string path,
        HttpContext http,
        CancellationToken ct
    )
    {
        switch (routes.StateOf(key))
        {
            case PluginAdminState.Unknown:
                return new(StatusCodes.Status404NotFound, $"There is no plugin {key}.");
            case PluginAdminState.Off:
                return new(
                    StatusCodes.Status410Gone,
                    $"The plugin {key} is turned off or was taken away. Reload the page."
                );
            case PluginAdminState.Reloading:
                return new(
                    StatusCodes.Status503ServiceUnavailable,
                    $"The plugin {key} is being reloaded. Try again in a moment."
                );
        }

        if (routes.Match(key, method, path, out var route) is not { } endpoint)
            return new(
                StatusCodes.Status404NotFound,
                $"The plugin {key} has no {method} /{path}. It may have changed since the page was opened: reload it."
            );

        if (!await HoldsAsync(http, endpoint.Node, ct).ConfigureAwait(false))
            return new(
                StatusCodes.Status403Forbidden,
                $"You can't do that: it needs {endpoint.Node}."
            );

        var (body, problem) = await ReadBodyAsync(http, ct).ConfigureAwait(false);

        if (problem is not null)
            return problem;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);

        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _config.PluginTimeoutSeconds)));

        var request = new PluginAdminRequest(
            method,
            path,
            route,
            http.Request.Query.ToDictionary(
                x => x.Key,
                x => x.Value.ToString(),
                StringComparer.OrdinalIgnoreCase
            ),
            body,
            AdminIdentity.Of(http).PlayerId.Value
        );

        PluginAdminResult result;

        try
        {
            // A plugin that ignores the token is not waited for past the timeout either.
            result = await endpoint
                .Handler(request, timeout.Token)
                .WaitAsync(timeout.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(
                StatusCodes.Status504GatewayTimeout,
                $"The plugin {key} did not answer within {_config.PluginTimeoutSeconds} s."
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "The plugin {Key} failed on {Method} /{Path}", key, method, path);

            return new(
                StatusCodes.Status500InternalServerError,
                $"The plugin {key} failed: {ex.Message}"
            );
        }

        return From(result);
    }

    /// <summary>The JSON body, if any; an answer instead when it is too large or not JSON.</summary>
    private async Task<(JsonElement? Body, Answer? Problem)> ReadBodyAsync(
        HttpContext http,
        CancellationToken ct
    )
    {
        var cap = Math.Max(0, _config.PluginMaxBodyBytes);
        var tooLarge = new Answer(
            StatusCodes.Status413PayloadTooLarge,
            $"The request is larger than the {cap} bytes a plugin is sent."
        );

        if (http.Request.ContentLength > cap)
            return (null, tooLarge);

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;

        while ((read = await http.Request.Body.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > cap)
                return (null, tooLarge);

            buffer.Write(chunk, 0, read);
        }

        if (buffer.Length == 0)
            return (null, null);

        try
        {
            using var document = JsonDocument.Parse(buffer.ToArray());

            return (document.RootElement.Clone(), null);
        }
        catch (JsonException)
        {
            return (null, new(StatusCodes.Status400BadRequest, "The request body is not JSON."));
        }
    }

    private static Answer From(PluginAdminResult result)
    {
        if (result.Error is { } error)
            return new(result.Status, error);

        if (result.File is { } file)
            return new(
                result.Status,
                null,
                Results.File(
                    file,
                    result.ContentType ?? "application/octet-stream",
                    result.FileName
                )
            );

        return new(
            result.Status,
            null,
            result.Status == StatusCodes.Status204NoContent
                ? Results.NoContent()
                : Results.Json(result.Value, statusCode: result.Status)
        );
    }

    private Task<bool> HoldsAsync(HttpContext http, string node, CancellationToken ct) =>
        grainFactory.HasPermissionAsync(AdminIdentity.Of(http).PlayerId, node, ct);

    private sealed record Answer(int Status, string? Error, IResult? Value = null);
}
