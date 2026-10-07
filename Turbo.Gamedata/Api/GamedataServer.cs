using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Snapshots;

namespace Turbo.Gamedata.Api;

/// <summary>
/// Serves the gamedata files the client loads, the way Habbo serves its own:
/// <list type="bullet">
/// <item><c>/gamedata/&lt;file&gt;/0</c> redirects (307) to the current build's address. This is
/// the address the client is configured with (<c>furnituredata.url</c>); it may be cached for
/// <see cref="GamedataConfig.CurrentMaxAgeSeconds"/>.</item>
/// <item><c>/gamedata/&lt;file&gt;/&lt;sha1&gt;</c> is a build by the hash of its content. It
/// never changes, so it is cached for good, and kept a while after a newer build.</item>
/// <item><c>/gamedata/hashes</c> lists each file's current hash, in Habbo's shape.</item>
/// </list>
/// Anyone may load them, from any site: they are what every player's client downloads. Its own
/// small web host beside the silo, off unless <see cref="GamedataConfig.Enabled"/>.
/// </summary>
internal sealed class GamedataServer(
    IServiceProvider services,
    IOptions<GamedataConfig> config,
    ILoggerFactory loggerFactory,
    ILogger<GamedataServer> logger
) : IHostedService, IAsyncDisposable
{
    private const string CURRENT = "0";
    private const string GZIP = "gzip";
    private const string IMMUTABLE = "public, max-age=31536000, immutable";
    private const int HASH_LENGTH = 40;

    private WebApplication? _app;

    public async Task StartAsync(CancellationToken ct)
    {
        var options = config.Value;

        if (!options.Enabled)
            return;

        var builder = WebApplication.CreateEmptyBuilder(new WebApplicationOptions());

        builder.WebHost.UseKestrelCore().UseUrls(options.Url);
        builder.Services.AddRoutingCore();
        builder.Services.AddCors(cors =>
            cors.AddDefaultPolicy(policy => policy.AllowAnyOrigin().WithMethods("GET"))
        );
        builder.Services.AddSingleton(loggerFactory);
        builder.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        var app = builder.Build();
        var files = services.GetRequiredService<IGamedataFileService>();

        // Behind the reverse proxy the request comes from loopback; its forwarded headers give
        // the scheme and host clients used, which the hashes list's addresses are built on.
        app.UseForwardedHeaders(
            new ForwardedHeadersOptions
            {
                ForwardedHeaders =
                    ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
            }
        );
        app.UseCors();
        app.Use(HandleFailureAsync);
        app.MapGet(
            "/gamedata/hashes",
            (HttpContext http, CancellationToken ct) => HashesAsync(http, files, ct)
        );
        app.MapGet(
            "/gamedata/{file}/{version}",
            (HttpContext http, string file, string version, CancellationToken ct) =>
                FileAsync(http, files, file, version, ct)
        );

        // The files are a convenience to the hotel's own process; an address it cannot have
        // leaves the hotel running without them rather than taking it down.
        try
        {
            await app.StartAsync(ct).ConfigureAwait(false);
        }
        catch (IOException ex)
        {
            logger.LogError(
                ex,
                "Gamedata host could not listen on {Url}, so the client's gamedata is unavailable; the hotel runs on without it. Free the port or set Turbo:Gamedata:Url, then restart.",
                options.Url
            );

            await app.DisposeAsync().ConfigureAwait(false);

            return;
        }

        _app = app;

        logger.LogInformation("Gamedata host listening on {Url}", options.Url);
    }

    public async Task StopAsync(CancellationToken ct)
    {
        if (_app is { } app)
            await app.StopAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_app is { } app)
            await app.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// Each file's current hash, as Habbo lists its own: <c>{ "hashes": [ { name, url, hash } ] }</c>,
    /// the url the file's address without its hash, and cached as briefly as <c>/0</c>.
    /// </summary>
    private async Task<IResult> HashesAsync(
        HttpContext http,
        IGamedataFileService files,
        CancellationToken ct
    )
    {
        var root = PublicRoot(http);
        var hashes = new List<object>();

        foreach (var file in GamedataFiles.ALL)
        {
            var current = await files.GetCurrentAsync(file, ct).ConfigureAwait(false);

            hashes.Add(
                new
                {
                    name = GamedataFiles.HashesName(file),
                    url = $"{root}/gamedata/{file}",
                    hash = current.File.Hash,
                }
            );
        }

        http.Response.Headers.CacheControl =
            $"no-transform, max-age={config.Value.CurrentMaxAgeSeconds}";

        return Results.Json(new { hashes }, statusCode: StatusCodes.Status200OK);
    }

    /// <summary>
    /// Where clients reach this host: <see cref="GamedataConfig.PublicUrl"/>, or the address the
    /// request came to (through the reverse proxy, which says so in its forwarded headers).
    /// </summary>
    private string PublicRoot(HttpContext http) =>
        string.IsNullOrWhiteSpace(config.Value.PublicUrl)
            ? $"{http.Request.Scheme}://{http.Request.Host}"
            : config.Value.PublicUrl.TrimEnd('/');

    private async Task<IResult> FileAsync(
        HttpContext http,
        IGamedataFileService files,
        string file,
        string version,
        CancellationToken ct
    )
    {
        if (!GamedataFiles.IsKnown(file))
            return Results.NotFound();

        if (version == CURRENT)
        {
            var current = await files.GetCurrentAsync(file, ct).ConfigureAwait(false);

            http.Response.Headers.CacheControl =
                $"public, max-age={config.Value.CurrentMaxAgeSeconds}";

            return Results.Redirect(
                $"/gamedata/{file}/{current.File.Hash}",
                permanent: false,
                preserveMethod: true
            );
        }

        if (!IsHash(version))
            return Results.NotFound();

        var content = await files.GetAsync(file, version, ct).ConfigureAwait(false);

        return content is null ? Results.NotFound() : Send(http, content);
    }

    /// <summary>The build as it is kept, gzipped, to a client that takes gzip; unzipped to one that doesn't.</summary>
    private static IResult Send(HttpContext http, GamedataFileContent content)
    {
        var headers = http.Response.Headers;
        var tag = $"\"{content.File.Hash}\"";

        headers.CacheControl = IMMUTABLE;
        headers.ETag = tag;
        headers.Vary = HeaderNames.AcceptEncoding;

        if (http.Request.Headers.IfNoneMatch.Contains(tag))
            return Results.StatusCode(StatusCodes.Status304NotModified);

        var acceptsGzip = http
            .Request.Headers.AcceptEncoding.SelectMany(x => (x ?? "").Split(','))
            .Any(x => x.Trim().StartsWith(GZIP, StringComparison.OrdinalIgnoreCase));

        if (acceptsGzip)
        {
            headers.ContentEncoding = GZIP;

            return Results.Bytes(content.Gzipped, GamedataFiles.ContentType(content.File.File));
        }

        return Results.Bytes(
            GamedataBytes.Decompress(content.Gzipped),
            GamedataFiles.ContentType(content.File.File)
        );
    }

    private static bool IsHash(string value) =>
        value.Length == HASH_LENGTH
        && value.All(c => char.IsAsciiHexDigitLower(c) || char.IsAsciiDigit(c));

    /// <summary>A failure is logged here with the request; the client gets a bare 500.</summary>
    private async Task HandleFailureAsync(HttpContext http, RequestDelegate next)
    {
        try
        {
            await next(http).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
        {
            // The client went away; nobody is waiting for an answer.
        }
        catch (Exception ex)
            when (!http.Response.HasStarted && !http.RequestAborted.IsCancellationRequested)
        {
            logger.LogError(
                ex,
                "Gamedata request {Method} {Path} failed",
                http.Request.Method,
                http.Request.Path
            );

            http.Response.StatusCode = StatusCodes.Status500InternalServerError;
        }
    }
}
