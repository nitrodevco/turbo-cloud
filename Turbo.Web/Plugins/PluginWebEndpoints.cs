using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Contracts.Plugins.Web;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Web;
using Turbo.Web.Api;
using Turbo.Web.Configuration;
using Turbo.Web.Sessions;

namespace Turbo.Web.Plugins;

/// <summary>
/// Plugins' player-facing routes (<see cref="IPluginWebRoute"/>) at
/// <c>/api/plugins/&lt;key&gt;/...</c>. The host stands between the caller and the plugin: the
/// address's rate (429), the plugin's state (503 reloading, 404 unknown, unloaded or no such
/// route), who is calling (the client's bearer token or the site's session cookie; 401 when a
/// route needs a player or a token ran out), the route's permission (403), the player's rate
/// (429), the body's size (413) and the time it takes (504); an exception is a 500 with the
/// plugin's key in the log, never its details. Pages of the hotel's own site and client, and the
/// sites the hotel adds, may call from a browser (CORS, without credentials: the client sends its
/// token); a call that changes something from any other site's page is refused (403).
/// </summary>
internal sealed class PluginWebEndpoints : IAsyncDisposable
{
    private static readonly string[] METHODS = ["GET", "POST", "PUT", "PATCH", "DELETE"];

    /// <summary>What signs the caller in, which a route is handed only when it asks.</summary>
    private static readonly HashSet<string> CREDENTIAL_HEADERS = new(
        ["Authorization", "Cookie", "Proxy-Authorization"],
        StringComparer.OrdinalIgnoreCase
    );

    private readonly IPluginWebRoutes _routes;
    private readonly IPlayerWebTokens _tokens;
    private readonly WebSessions _sessions;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<PluginWebEndpoints> _logger;
    private readonly PluginRoutesConfig _config;
    private readonly HashSet<string> _origins;
    private readonly PartitionedRateLimiter<string> _perAddress;
    private readonly PartitionedRateLimiter<string> _perPlayer;

    public PluginWebEndpoints(
        IPluginWebRoutes routes,
        IPlayerWebTokens tokens,
        WebSessions sessions,
        IGrainFactory grainFactory,
        IOptions<WebConfig> config,
        ILogger<PluginWebEndpoints> logger
    )
    {
        _routes = routes;
        _tokens = tokens;
        _sessions = sessions;
        _grainFactory = grainFactory;
        _logger = logger;
        _config = config.Value.PluginRoutes;
        _origins = AllowedOrigins(config.Value);
        _perAddress = PerMinute(_config.RequestsPerMinutePerAddress);
        _perPlayer = PerMinute(_config.RequestsPerMinutePerPlayer);
    }

    public void Map(IEndpointRouteBuilder api)
    {
        if (!_config.Enabled)
            return;

        api.MapMethods("/plugins/{key}/{**path}", METHODS, CallAsync);
        api.MapMethods("/plugins/{key}/{**path}", ["OPTIONS"], Preflight);
    }

    /// <summary>The origins of the hotel's site and client, and the ones it adds.</summary>
    private static HashSet<string> AllowedOrigins(WebConfig web)
    {
        var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (
            var url in new[]
            {
                web.SiteUrl,
                web.ClientUrl.Replace("{ticket}", "x", StringComparison.Ordinal),
                web.PluginRoutes.PublicUrl,
            }.Concat(web.PluginRoutes.AllowedOrigins)
        )
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme.StartsWith("http"))
                origins.Add(uri.GetLeftPart(UriPartial.Authority));

        return origins;
    }

    private static PartitionedRateLimiter<string> PerMinute(int permits) =>
        PartitionedRateLimiter.Create<string, string>(partition =>
            RateLimitPartition.GetFixedWindowLimiter(
                partition,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, permits),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }
            )
        );

    /// <summary>Lets an allowed site's page read the answer; whether the request came from one (or from no page).</summary>
    private bool AllowOrigin(HttpContext http)
    {
        var origin = http.Request.Headers.Origin.ToString();

        if (origin.Length == 0)
            return true;

        if (!_origins.Contains(origin))
            return false;

        var headers = http.Response.Headers;

        headers.AccessControlAllowOrigin = origin;
        headers.Vary = "Origin";
        headers.AccessControlExposeHeaders = "Retry-After";

        return true;
    }

    private IResult Preflight(string key, string? path, HttpContext http)
    {
        if (!AllowOrigin(http) || http.Request.Headers.Origin.Count == 0)
            return Results.NoContent();

        var methods = _routes.MethodsAt(key, path ?? string.Empty);

        if (methods.Count == 0)
            return Results.NoContent();

        var headers = http.Response.Headers;

        headers.AccessControlAllowMethods = string.Join(", ", methods);
        headers.AccessControlAllowHeaders = "Authorization, Content-Type";
        headers.AccessControlMaxAge = "600";

        return Results.NoContent();
    }

    private async Task<IResult> CallAsync(
        string key,
        string? path,
        HttpContext http,
        CancellationToken ct
    )
    {
        var method = http.Request.Method.ToUpperInvariant();
        var address = http.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

        path ??= string.Empty;

        if (!AllowOrigin(http) && method != "GET")
            return Error(
                StatusCodes.Status403Forbidden,
                "This site may not call the hotel's plugins."
            );

        if (Limited(http, _perAddress, address) is { } addressLimited)
            return addressLimited;

        if (_routes.IsReloading(key))
        {
            http.Response.Headers.RetryAfter = "2";

            return Error(
                StatusCodes.Status503ServiceUnavailable,
                $"{key} is being updated. Try again in a moment."
            );
        }

        if (_routes.Match(key, method, path, out var route) is not { } endpoint)
            return Error(StatusCodes.Status404NotFound, "There is nothing here.");

        var (player, signedOut) = await CallerAsync(http, ct).ConfigureAwait(false);

        if (signedOut || (player is null && !endpoint.Anonymous))
            return Error(StatusCodes.Status401Unauthorized, "Sign in first.");

        if (player is { } id)
        {
            if (!await HoldsAnyAsync(id, endpoint, ct).ConfigureAwait(false))
                return Error(StatusCodes.Status403Forbidden, "You can't do that.");

            if (Limited(http, _perPlayer, id.ToString()) is { } playerLimited)
                return playerLimited;
        }

        var (body, raw, problem) = await ReadBodyAsync(http, ct).ConfigureAwait(false);

        if (problem is not null)
            return problem;

        var timeoutSeconds = Math.Max(1, _config.TimeoutSeconds);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);

        timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var request = new PluginWebRequest(
            method,
            path,
            route,
            http.Request.Query.ToDictionary(
                x => x.Key,
                x => x.Value.ToString(),
                StringComparer.OrdinalIgnoreCase
            ),
            body,
            player?.Value,
            address
        )
        {
            Headers = HeadersFor(http, endpoint.Route.CredentialHeaders),
            RawBody = raw,
        };

        PluginWebResult result;

        try
        {
            // A plugin that ignores the token is not waited for past the timeout either.
            result = await endpoint
                .Route.HandleAsync(request, timeout.Token)
                .WaitAsync(timeout.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "The plugin {Key} did not answer {Method} /{Path} within {Seconds} s",
                key,
                method,
                path,
                timeoutSeconds
            );

            return Error(StatusCodes.Status504GatewayTimeout, "That took too long. Try again.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "The plugin {Key} failed on {Method} /{Path}", key, method, path);

            return Error(StatusCodes.Status500InternalServerError, "Something went wrong.");
        }

        if (result.Error is { } error)
            return Error(result.Status, error);

        return result.Status == StatusCodes.Status204NoContent
            ? Results.NoContent()
            : Results.Json(result.Value, statusCode: result.Status);
    }

    /// <summary>
    /// The player calling: the client's token, else the site's session. <c>SignedOut</c> when a
    /// token was sent that is not (or no longer) one, so the client asks for a new one.
    /// </summary>
    private async Task<(PlayerId? Player, bool SignedOut)> CallerAsync(
        HttpContext http,
        CancellationToken ct
    )
    {
        var authorization = http.Request.Headers.Authorization.ToString();

        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return _tokens.Find(authorization["Bearer ".Length..].Trim()) is { } player
                ? (player, false)
                : (null, true);

        return (
            await _sessions
                .FindAsync(WebCookies.Get(http, WebCookies.SESSION), ct)
                .ConfigureAwait(false),
            false
        );
    }

    /// <summary>Whether the player holds any one of the route's nodes; true for a route that needs none.</summary>
    private async Task<bool> HoldsAnyAsync(
        PlayerId player,
        PluginWebEndpoint endpoint,
        CancellationToken ct
    )
    {
        if (endpoint.Nodes.IsDefaultOrEmpty)
            return true;

        foreach (var node in endpoint.Nodes)
            if (await _grainFactory.HasPermissionAsync(player, node, ct).ConfigureAwait(false))
                return true;

        return false;
    }

    private static IResult? Limited(
        HttpContext http,
        PartitionedRateLimiter<string> limiter,
        string partition
    )
    {
        using var lease = limiter.AttemptAcquire(partition);

        if (lease.IsAcquired)
            return null;

        http.Response.Headers.RetryAfter = "60";

        return Error(StatusCodes.Status429TooManyRequests, "Slow down a little.");
    }

    /// <summary>
    /// The request's headers by lower-case name; the caller's credentials only for a route that
    /// asks for them.
    /// </summary>
    private static Dictionary<string, string> HeadersFor(HttpContext http, bool credentials)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (name, value) in http.Request.Headers)
            if (credentials || !CREDENTIAL_HEADERS.Contains(name))
                headers[name.ToLowerInvariant()] = value.ToString();

        return headers;
    }

    /// <summary>
    /// The JSON body, if any, and its bytes as sent; an answer instead when it is too large or
    /// not JSON.
    /// </summary>
    private async Task<(
        JsonElement? Body,
        ReadOnlyMemory<byte> Raw,
        IResult? Problem
    )> ReadBodyAsync(HttpContext http, CancellationToken ct)
    {
        var cap = Math.Max(0, _config.MaxBodyBytes);
        var tooLarge = Error(
            StatusCodes.Status413PayloadTooLarge,
            $"That is more than the {cap} bytes the hotel takes."
        );

        if (http.Request.ContentLength > cap)
            return (null, default, tooLarge);

        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;

        while ((read = await http.Request.Body.ReadAsync(chunk, ct).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > cap)
                return (null, default, tooLarge);

            await buffer.WriteAsync(chunk.AsMemory(0, read), ct).ConfigureAwait(false);
        }

        if (buffer.Length == 0)
            return (null, default, null);

        var raw = buffer.ToArray();

        try
        {
            using var document = JsonDocument.Parse(raw);

            return (document.RootElement.Clone(), raw, null);
        }
        catch (JsonException)
        {
            return (
                null,
                default,
                Error(StatusCodes.Status400BadRequest, "The request body is not JSON.")
            );
        }
    }

    private static IResult Error(int status, string error) =>
        Results.Json(new { error }, statusCode: status);

    public async ValueTask DisposeAsync()
    {
        await _perAddress.DisposeAsync().ConfigureAwait(false);
        await _perPlayer.DisposeAsync().ConfigureAwait(false);
    }
}
