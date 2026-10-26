using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Turbo.Gamedata.Configuration;
using Turbo.Primitives.Authentication;
using Turbo.Primitives.Camera;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Gamedata.Snapshots;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;
using Turbo.Primitives.Rooms;

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
/// <item><c>/gamedata/photos/&lt;photo id&gt;.png</c> (and <c>_small.png</c>) is a camera photo
/// (<see cref="ICameraPhotoFiles"/>), which the photo dialog and a photo poster load: the client's
/// <c>stories.image_url_base</c> is <c>/gamedata/</c> on this host, as the photo's url is
/// <c>photos/&lt;id&gt;.png</c>. Under <c>/gamedata/</c>, so the reverse proxy that passes the gamedata on
/// passes it too. A photo never changes (a new one has a new id), so it is cached for good.</item>
/// <item><c>/gamedata/photodata/&lt;photo id&gt;</c> is a bought photo's extra data
/// (<see cref="ICameraPhotoFiles.GetExtraDataFile"/>), which a poster's viewer loads: the client's
/// <c>extra_data_service_url</c> is this address without the id, as Habbo's is its
/// <c>.../photodata/public/furni/</c>. A <c>POST</c> of a JSON array of ids to the same address
/// without an id answers each known photo's <c>{ id, url }</c>, which posters on the wall ask
/// for together under <c>extra_data_batches_enabled</c> (<c>ExtraDataManager.handleBatch</c>).</item>
/// <item><c>/gamedata/starter-rooms/&lt;room id&gt;.png</c> is a starter room's picture
/// (<see cref="IStarterRoomDirectory"/>), which the room picker shows: the client's
/// <c>new.user.flow.room.image.url</c> is this address with <c>%type%</c> for the id. Under
/// <c>/gamedata/</c>, so the reverse proxy that passes the gamedata on passes it too. It can be
/// taken again, so it is cached for a minute only. <c>/starter-rooms/&lt;room id&gt;.png</c>, where it
/// was served before, still answers for a client holding the old address.</item>
/// <item><c>/gamedata/navigator-thumbnail/&lt;room id&gt;.png</c> is a room's thumbnail
/// (<see cref="IRoomThumbnailFiles"/>), the picture the navigator shows: the client's
/// <c>navigator.thumbnail.url_base</c> is this address without the room id, as Habbo's is its
/// <c>.../navigator-thumbnail/hhus/</c>. Under <c>/gamedata/</c>, so the reverse proxy that passes
/// the gamedata on passes it too. Its owner can take it again at any time, so the browser asks
/// again each time it shows it (<c>no-cache</c>, answered 304 while it is the same), the 404 of a
/// room without one included.</item>
/// <item><c>/gamedata/plugins/&lt;key&gt;/&lt;path&gt;</c> is a file of a plugin's client module
/// (<see cref="IClientModuleDirectory"/>), from the plugin folder's <c>wwwroot/</c>: GET only, and
/// never a hidden file or one outside the folder. Asked for with <c>?v=</c> its own hash's start, as
/// the module's address is, it is cached for good; otherwise the browser asks again each time
/// (<c>no-cache</c>, answered 304 while it is the same), so a module's own imports always load the
/// files beside it. Under <c>/gamedata/</c>, so the reverse proxy that passes the gamedata on
/// passes it too.</item>
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

    /// <summary>Where a camera photo is served, before <c>&lt;photo id&gt;.png</c>.</summary>
    public const string PHOTO_ROUTE = "/gamedata/photos/";

    /// <summary>Where a starter room's picture is served, before <c>&lt;room id&gt;.png</c>.</summary>
    public const string STARTER_ROOM_PICTURE_ROUTE = "/gamedata/starter-rooms/";

    /// <summary>Where a starter room's picture was served before <see cref="STARTER_ROOM_PICTURE_ROUTE"/>.</summary>
    public const string OLD_STARTER_ROOM_PICTURE_ROUTE = "/starter-rooms/";
    private const string STARTER_ROOM_PICTURE_CACHE = "public, max-age=60";
    private const string ROOM_THUMBNAIL_CACHE = "public, no-cache";
    private const string PHOTO_DATA_CACHE = "no-store";
    private const string JSON = "application/json; charset=utf-8";

    /// <summary>Where a photo's extra data is served, before <c>&lt;photo id&gt;</c>.</summary>
    public const string PHOTO_DATA_ROUTE = "/gamedata/photodata/";

    /// <summary>The most ids a batch asks for (<c>ExtraDataManager.BATCH_MAX_QUERY_AMOUNT</c>); more are not answered.</summary>
    public const int PHOTO_DATA_BATCH_MAX = 50;

    /// <summary>Where a room's thumbnail is served, before <c>&lt;room id&gt;.png</c>.</summary>
    public const string ROOM_THUMBNAIL_ROUTE = "/gamedata/navigator-thumbnail/";

    /// <summary>Where a client module's files are served, before <c>&lt;plugin key&gt;/&lt;path&gt;</c>.</summary>
    public const string PLUGIN_FILES_ROUTE = "/gamedata/plugins/";
    private const string PLUGIN_FILE_CACHE = "public, no-cache";

    /// <summary>
    /// Where a client asks, before it connects, which theme its player draws: a <c>POST</c> of
    /// <c>{ "ticket": "...", "theme": "<staff's preview>" }</c>, the ticket it will log in with
    /// (looked at, not used up), answered <c>{ theme, variables, choices, chosen }</c>
    /// (<see cref="IThemeAudiences.SessionAsync"/>; <c>variables</c> null when they are the file's
    /// own). A ticket that is no player's answers the hotel's default. Never cached: it is a player's.
    /// </summary>
    public const string THEME_ROUTE = "/gamedata/theme";

    private static readonly FileExtensionContentTypeProvider CONTENT_TYPES = new();

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
            cors.AddDefaultPolicy(policy =>
                policy
                    .AllowAnyOrigin()
                    .WithMethods("GET", "POST")
                    .WithHeaders(HeaderNames.ContentType)
            )
        );
        builder.Services.AddSingleton(loggerFactory);
        builder.Services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

        var app = builder.Build();
        var files = services.GetRequiredService<IGamedataFileService>();
        var photos = services.GetService<ICameraPhotoFiles>();
        var starterRooms = services.GetService<IStarterRoomDirectory>();
        var thumbnails = services.GetService<IRoomThumbnailFiles>();
        var modules = services.GetService<IClientModuleDirectory>();
        var themes = services.GetService<IThemeAudiences>();
        var tickets = services.GetService<IAuthenticationService>();

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
        app.Use(AllowAnyOriginAsync);
        app.Use(HandleFailureAsync);
        app.MapGet(
            "/gamedata/hashes",
            (HttpContext http, CancellationToken ct) => HashesAsync(http, files, ct)
        );
        if (photos is not null)
            app.MapGet(
                PHOTO_ROUTE + "{name}.png",
                (HttpContext http, string name) => Photo(http, photos, name)
            );
        if (photos is not null)
        {
            app.MapGet(
                PHOTO_DATA_ROUTE + "{id}",
                (HttpContext http, string id) => PhotoData(http, photos, id)
            );
            app.MapPost(
                PHOTO_DATA_ROUTE,
                (HttpContext http, CancellationToken ct) => PhotoDataBatchAsync(http, photos, ct)
            );
        }
        if (starterRooms is not null)
        {
            app.MapGet(
                STARTER_ROOM_PICTURE_ROUTE + "{roomId:int}.png",
                (HttpContext http, int roomId) => StarterRoomPicture(http, starterRooms, roomId)
            );
            app.MapGet(
                OLD_STARTER_ROOM_PICTURE_ROUTE + "{roomId:int}.png",
                (HttpContext http, int roomId) => StarterRoomPicture(http, starterRooms, roomId)
            );
        }
        if (thumbnails is not null)
            app.MapGet(
                ROOM_THUMBNAIL_ROUTE + "{roomId:int}.png",
                (HttpContext http, int roomId) => RoomThumbnail(http, thumbnails, roomId)
            );
        if (modules is not null)
            app.MapGet(
                PLUGIN_FILES_ROUTE + "{key}/{**path}",
                (HttpContext http, string key, string path) => PluginFile(http, modules, key, path)
            );
        if (themes is not null && tickets is not null)
            app.MapPost(
                THEME_ROUTE,
                (HttpContext http, CancellationToken ct) => ThemeAsync(http, themes, tickets, ct)
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

    /// <summary>A camera photo, as the renderer drew it; 404 when there is none.</summary>
    private static IResult Photo(HttpContext http, ICameraPhotoFiles photos, string name)
    {
        var file = photos.GetPhotoFile(name);

        if (file is null)
            return Results.NotFound();

        http.Response.Headers.CacheControl = IMMUTABLE;

        return Results.File(Path.GetFullPath(file), "image/png");
    }

    /// <summary>
    /// A bought photo's extra data, as it was kept when it was bought; 404 when there is none. Not
    /// cached, as Habbo's (<c>no-store</c>, official client capture).
    /// </summary>
    private static IResult PhotoData(HttpContext http, ICameraPhotoFiles photos, string id)
    {
        var file = photos.GetExtraDataFile(id);

        if (file is null)
            return Results.NotFound();

        http.Response.Headers.CacheControl = PHOTO_DATA_CACHE;

        return Results.File(Path.GetFullPath(file), JSON);
    }

    /// <summary>
    /// The urls of the photos a batch names, <c>[ { id, url } ]</c>, the ones known only: what
    /// <c>ExtraDataManager.onExtraDataLoaded</c> hands each poster by its id. Not a JSON array of
    /// strings is a 400.
    /// </summary>
    private static async Task<IResult> PhotoDataBatchAsync(
        HttpContext http,
        ICameraPhotoFiles photos,
        CancellationToken ct
    )
    {
        string[]? ids;

        try
        {
            ids = await JsonSerializer
                .DeserializeAsync<string[]>(http.Request.Body, cancellationToken: ct)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        if (ids is null)
            return Results.BadRequest();

        var answers = new List<object>();

        foreach (var id in ids.Take(PHOTO_DATA_BATCH_MAX).Distinct(StringComparer.Ordinal))
        {
            if (id is null || photos.GetExtraDataFile(id) is not { } file)
                continue;

            using var data = JsonDocument.Parse(
                await File.ReadAllTextAsync(file, ct).ConfigureAwait(false)
            );

            if (data.RootElement.TryGetProperty("url", out var url))
                answers.Add(new { id, url = url.GetString() });
        }

        http.Response.Headers.CacheControl = PHOTO_DATA_CACHE;

        return Results.Json(answers);
    }

    /// <summary>The theme the ticket's player draws (<see cref="THEME_ROUTE"/>); a body that is not the request's JSON is a 400.</summary>
    private static async Task<IResult> ThemeAsync(
        HttpContext http,
        IThemeAudiences themes,
        IAuthenticationService tickets,
        CancellationToken ct
    )
    {
        ThemeRequest? request;

        try
        {
            request = await JsonSerializer
                .DeserializeAsync<ThemeRequest>(http.Request.Body, THEME_JSON, ct)
                .ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return Results.BadRequest();
        }

        if (request is null)
            return Results.BadRequest();

        var player = await tickets
            .FindPlayerIdFromTicketAsync(request.Ticket ?? string.Empty, ct)
            .ConfigureAwait(false);
        var session = await themes
            .SessionAsync(new PlayerId(player), player > 0 ? request.Theme : null, ct)
            .ConfigureAwait(false);

        http.Response.Headers.CacheControl = PHOTO_DATA_CACHE;

        return Results.Json(
            new
            {
                theme = session.Theme,
                variables = session.Variables,
                choices = session.Choices.Select(x => new { key = x.Key, name = x.Name }),
                chosen = session.Chosen,
            }
        );
    }

    private static readonly JsonSerializerOptions THEME_JSON = new(JsonSerializerDefaults.Web);

    private sealed record ThemeRequest(string? Ticket, string? Theme);

    /// <summary>A starter room's picture, as the starter room camera last drew it; 404 when there is none.</summary>
    private static IResult StarterRoomPicture(
        HttpContext http,
        IStarterRoomDirectory starterRooms,
        int roomId
    )
    {
        var file = starterRooms.GetPictureFile(new RoomId(roomId));

        if (file is null)
            return Results.NotFound();

        http.Response.Headers.CacheControl = STARTER_ROOM_PICTURE_CACHE;

        return Results.File(Path.GetFullPath(file), "image/png");
    }

    /// <summary>A room's thumbnail, as the room thumbnail camera last drew it; 404 when there is none.</summary>
    private static IResult RoomThumbnail(
        HttpContext http,
        IRoomThumbnailFiles thumbnails,
        int roomId
    )
    {
        var file = thumbnails.GetThumbnailFile(new RoomId(roomId));

        // The 404 too: the room info asks for it before the owner takes the first one, and a cache
        // keeping that answer would hide the picture once it exists.
        http.Response.Headers.CacheControl = ROOM_THUMBNAIL_CACHE;

        if (file is null)
            return Results.NotFound();

        return Results.File(
            Path.GetFullPath(file),
            "image/png",
            lastModified: File.GetLastWriteTimeUtc(file)
        );
    }

    /// <summary>
    /// A file of a plugin's client module; 404 when there is none. Cached for good when asked for
    /// by its own hash, as the module's address is; otherwise asked for again each time.
    /// </summary>
    internal static IResult PluginFile(
        HttpContext http,
        IClientModuleDirectory modules,
        string key,
        string path
    )
    {
        var file = modules.FindFile(key, path);

        if (file is null)
            return Results.NotFound();

        var hash = modules.HashOf(file);
        var version = http.Request.Query["v"].ToString();
        var headers = http.Response.Headers;
        var tag = $"\"{hash}\"";

        headers.CacheControl =
            version.Length > 0 && hash.StartsWith(version, StringComparison.Ordinal)
                ? IMMUTABLE
                : PLUGIN_FILE_CACHE;
        headers.ETag = tag;

        if (http.Request.Headers.IfNoneMatch.Contains(tag))
            return Results.StatusCode(StatusCodes.Status304NotModified);

        return Results.File(
            file,
            CONTENT_TYPES.TryGetContentType(file, out var type) ? type : "application/octet-stream"
        );
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

    /// <summary>
    /// Every response says any site may read it, asked by a site or not. The CORS middleware
    /// says so only to a request with an <c>Origin</c>, and a build is cached for good: one first
    /// loaded without (opened in a tab) would stay in the browser without the header, and the
    /// client's later fetch of the same address be refused.
    /// </summary>
    private static Task AllowAnyOriginAsync(HttpContext http, RequestDelegate next)
    {
        http.Response.Headers.AccessControlAllowOrigin = "*";

        return next(http);
    }

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
