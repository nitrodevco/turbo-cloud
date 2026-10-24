using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Wired.WebApi;
using Turbo.Web.Configuration;

namespace Turbo.Web.Api;

/// <summary>
/// The public Web API's Wired Variables endpoints (<c>/api/public/rooms/{roomId}/variables...</c>,
/// Habbo's OpenAPI document, tag "Wired Variables"), so a client library written for Habbo works
/// with this hotel's address. Each call is counted against its room's limit for its kind, checked
/// to name a room, and answered by the room itself, which holds the add-on's keys.
/// </summary>
internal sealed class WiredVariablesEndpoints(
    IGrainFactory grainFactory,
    IDbContextFactory<TurboDbContext> dbContextFactory,
    IOptions<WebConfig> config,
    TimeProvider timeProvider
)
{
    private const string READ_KEY_HEADER = "X-Wired-Read-Key";
    private const string WRITE_KEY_HEADER = "X-Wired-Write-Key";

    private readonly WiredApiRateLimits _limits = new(config.Value.WiredApi, timeProvider);

    private WiredApiConfig Api => config.Value.WiredApi;

    public void Map(IEndpointRouteBuilder api)
    {
        if (!Api.Enabled)
            return;

        var rooms = api.MapGroup("/public/rooms/{roomId:int}");

        rooms.MapGet("/variables", Handle(WiredWebApiOperation.ListVariables));
        rooms.MapPost("/variables/bulk-delete", Handle(WiredWebApiOperation.BulkDelete));
        rooms.MapGet("/variables/global/{variableName}", Handle(WiredWebApiOperation.GetGlobal));
        rooms.MapPatch(
            "/variables/global/{variableName}",
            Handle(WiredWebApiOperation.PatchGlobal)
        );
        rooms.MapPost(
            "/variables/{scope}/{variableName}/batch",
            Handle(WiredWebApiOperation.Batch)
        );
        rooms.MapGet(
            "/variables/{scope}/{variableName}/{targetKind}",
            Handle(WiredWebApiOperation.ListValues)
        );
        rooms.MapGet(
            "/variables/{scope}/{variableName}/{targetKind}/count",
            Handle(WiredWebApiOperation.CountValues)
        );

        const string VALUE = "/variables/{scope}/{variableName}/{targetKind}/{entityId}";

        rooms.MapGet(VALUE, Handle(WiredWebApiOperation.GetValue));
        rooms.MapPut(VALUE, Handle(WiredWebApiOperation.PutValue));
        rooms.MapPatch(VALUE, Handle(WiredWebApiOperation.PatchValue));
        rooms.MapDelete(VALUE, Handle(WiredWebApiOperation.DeleteValue));

        rooms.MapGet("/variables_profile/global", Handle(WiredWebApiOperation.GetGlobalProfile));
        rooms.MapPatch(
            "/variables_profile/global",
            Handle(WiredWebApiOperation.PatchGlobalProfile)
        );
        rooms.MapGet("/variables_profile/user/users", Handle(WiredWebApiOperation.FindUserProfile));

        const string USER_PROFILE = "/variables_profile/user/{targetKind}/{entityId}";
        const string FURNI_PROFILE = "/variables_profile/furni/{targetKind}/{entityId}";

        rooms.MapGet(USER_PROFILE, Handle(WiredWebApiOperation.GetUserProfile));
        rooms.MapPatch(USER_PROFILE, Handle(WiredWebApiOperation.PatchUserProfile));
        rooms.MapDelete(USER_PROFILE, Handle(WiredWebApiOperation.DeleteUserProfile));
        rooms.MapGet(FURNI_PROFILE, Handle(WiredWebApiOperation.GetFurniProfile));
        rooms.MapPatch(FURNI_PROFILE, Handle(WiredWebApiOperation.PatchFurniProfile));

        // A browser asks before a call with key headers from another site (a CORS preflight).
        rooms.MapMethods("/variables/{**path}", [HttpMethods.Options], PreflightAsync);
        rooms.MapMethods("/variables_profile/{**path}", [HttpMethods.Options], PreflightAsync);
    }

    private RequestDelegate Handle(WiredWebApiOperation operation) =>
        http =>
        {
            AllowOrigin(http);

            return HandleAsync(http, operation, http.RequestAborted);
        };

    /// <summary>
    /// Answers a browser's preflight: from an allowed site (<see cref="WiredApiConfig.AllowedOrigins"/>)
    /// every method and key header of these endpoints; from any other, nothing, so the browser
    /// does not make the call.
    /// </summary>
    private Task PreflightAsync(HttpContext http)
    {
        if (AllowOrigin(http))
        {
            var headers = http.Response.Headers;

            headers.AccessControlAllowMethods = "GET, PUT, PATCH, DELETE, POST";
            headers.AccessControlAllowHeaders =
                $"{READ_KEY_HEADER}, {WRITE_KEY_HEADER}, Content-Type";
            headers.AccessControlMaxAge = "600";
        }

        http.Response.StatusCode = StatusCodes.Status204NoContent;

        return Task.CompletedTask;
    }

    private bool AllowOrigin(HttpContext http) => ApiCors.AllowOrigin(http, Api.AllowedOrigins);

    private async Task HandleAsync(
        HttpContext http,
        WiredWebApiOperation operation,
        CancellationToken ct
    )
    {
        var route = http.Request.RouteValues;
        var roomId = int.Parse((string)route["roomId"]!);
        var caller = http.Connection.RemoteIpAddress?.ToString() ?? string.Empty;

        if (_limits.FailureRetryAfter(caller) is { } failuresRetryAfter)
        {
            await TooManyRequestsAsync(http, failuresRetryAfter).ConfigureAwait(false);

            return;
        }

        var readKey = Header(http, READ_KEY_HEADER);
        var writeKey = Header(http, WRITE_KEY_HEADER);

        if (
            await RefusalAsync(roomId, operation, readKey, writeKey, ct).ConfigureAwait(false) is
            { } refusal
        )
        {
            _limits.CountFailure(caller);

            await WriteAsync(http, WiredWebApiErrors.StatusOf(refusal), Error(refusal))
                .ConfigureAwait(false);

            return;
        }

        if (_limits.TryAcquire(roomId, operation) is { } retryAfter)
        {
            await TooManyRequestsAsync(http, retryAfter).ConfigureAwait(false);

            return;
        }

        var (body, tooLarge) = await ReadBodyAsync(http, ct).ConfigureAwait(false);

        if (tooLarge)
        {
            await WriteAsync(http, StatusCodes.Status413PayloadTooLarge, null)
                .ConfigureAwait(false);

            return;
        }

        var query = http.Request.Query;
        var (playerId, playerName) = await FindPlayerAsync(
                operation,
                route["targetKind"] as string,
                route["entityId"] as string,
                query["name"],
                query["unique_id"],
                ct
            )
            .ConfigureAwait(false);
        var request = new WiredWebApiRequest
        {
            Operation = operation,
            ReadKey = readKey,
            WriteKey = writeKey,
            Scope = route["scope"] as string ?? string.Empty,
            VariableName = route["variableName"] as string ?? string.Empty,
            TargetKind = route["targetKind"] as string ?? string.Empty,
            EntityId = route["entityId"] as string ?? string.Empty,
            Body = body,
            Page = Number(query["page"]),
            Size = Number(query["size"]),
            OrderBy = query["order_by"],
            OrderDir = query["order_dir"],
            Name = query["name"],
            UniqueId = query["unique_id"],
            PlayerId = playerId,
            PlayerName = playerName,
            BatchWriteBudget = Api.BatchWriteBudgetPerMinute,
        };
        var response = await grainFactory
            .GetRoomGrain(roomId)
            .HandleWiredWebApiAsync(request, ct)
            .ConfigureAwait(false);

        await WriteAsync(http, response.Status, response.Json).ConfigureAwait(false);
    }

    /// <summary>
    /// Why a call is refused before its room answers it (a <see cref="WiredWebApiErrors"/> code),
    /// null when its keys open the room. A room that is loaded checks the keys itself, as they
    /// stand this moment; one that is not is only woken for a call carrying its add-on's keys as
    /// they were stored when it last unloaded, so a caller without them never loads a room.
    /// </summary>
    private async Task<string?> RefusalAsync(
        int roomId,
        WiredWebApiOperation operation,
        string? readKey,
        string? writeKey,
        CancellationToken ct
    )
    {
        if (roomId <= 0)
            return WiredWebApiErrors.ROOM_NOT_FOUND;

        var db = await dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        if (!await db.Rooms.AnyAsync(room => room.Id == roomId, ct).ConfigureAwait(false))
            return WiredWebApiErrors.ROOM_NOT_FOUND;

        var (reads, writes) = WiredWebApiKeys.KeysNeeded(operation);

        if ((reads && string.IsNullOrEmpty(readKey)) || (writes && string.IsNullOrEmpty(writeKey)))
            return WiredWebApiErrors.KEY_MISSING;

        var loaded = await grainFactory
            .GetRoomDirectoryGrain()
            .GetActiveRoomIdsAsync(ct)
            .ConfigureAwait(false);

        if (loaded.Contains(new RoomId(roomId)))
            return await grainFactory
                .GetRoomGrain(roomId)
                .CheckWiredWebApiKeysAsync(operation, readKey, writeKey, ct)
                .ConfigureAwait(false);

        // The add-on placed first answers, as in the room (RoomWiredSystem.OpenWebApiAsync).
        var stored = await db
            .Furnitures.AsNoTracking()
            .Where(item =>
                item.RoomEntityId == roomId
                && item.FurnitureDefinitionEntity!.Logic == WiredWebApiKeys.ADDON_LOGIC
            )
            .OrderBy(item => item.Id)
            .Select(item => new { item.ExtraData })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (stored is null)
            return WiredWebApiErrors.API_DISABLED;

        var keys = WiredWebApiKeys.StoredStringParam(stored.ExtraData);

        if (
            (reads && !WiredWebApiKeys.Matches(readKey, WiredWebApiKeys.ReadKeyOf(keys)))
            || (writes && !WiredWebApiKeys.Matches(writeKey, WiredWebApiKeys.WriteKeyOf(keys)))
        )
            return WiredWebApiErrors.KEY_INVALID;

        return null;
    }

    /// <summary>
    /// The player a user profile call names, by name, unique id (this hotel has none, so its
    /// player id, inference) or the player id in the path, so the room can answer for a player
    /// who is not there with their name. Nothing for other calls.
    /// </summary>
    private async Task<(int? Id, string? Name)> FindPlayerAsync(
        WiredWebApiOperation operation,
        string? targetKind,
        string? entityId,
        string? name,
        string? uniqueId,
        CancellationToken ct
    )
    {
        int? byId = null;
        string? byName = null;

        switch (operation)
        {
            case WiredWebApiOperation.FindUserProfile when !string.IsNullOrEmpty(name):
                byName = PlayerNames.Key(name);
                break;
            case WiredWebApiOperation.FindUserProfile when int.TryParse(uniqueId, out var id):
                byId = id;
                break;
            case WiredWebApiOperation.GetUserProfile
            or WiredWebApiOperation.PatchUserProfile
            or WiredWebApiOperation.DeleteUserProfile
                when targetKind == "users" && int.TryParse(entityId, out var id):
                byId = id;
                break;
            default:
                return (null, null);
        }

        var db = await dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var players = db.Players.AsNoTracking();
        var player = await (
            byName is not null
                ? players.Where(x => x.NameKey == byName)
                : players.Where(x => x.Id == byId)
        )
            .Select(x => new { x.Id, x.Name })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        return player is null ? (null, null) : (player.Id, player.Name);
    }

    /// <summary>A query number; one that is sent but is not a number reads as 0, which no list takes.</summary>
    private static int? Number(string? value) =>
        value is null ? null
        : int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
        : 0;

    private static string? Header(HttpContext http, string name) =>
        http.Request.Headers.TryGetValue(name, out var value) ? value.ToString() : null;

    /// <summary>
    /// The body of a POST, PUT or PATCH, read up to <see cref="WiredApiConfig.MaxBodyBytes"/>
    /// bytes. A longer one is not cut short (which would leave a different JSON document, or a
    /// broken one) but refused whole.
    /// </summary>
    private async Task<(string? Body, bool TooLarge)> ReadBodyAsync(
        HttpContext http,
        CancellationToken ct
    )
    {
        if (
            http.Request.ContentLength is 0
            || !(
                HttpMethods.IsPost(http.Request.Method)
                || HttpMethods.IsPut(http.Request.Method)
                || HttpMethods.IsPatch(http.Request.Method)
            )
        )
            return (null, false);

        var limit = Api.MaxBodyBytes;

        if (http.Request.ContentLength > limit)
            return (null, true);

        // One byte past the limit tells a body that is too long from one that just fits.
        var buffer = new byte[limit + 1];
        var read = 0;

        while (read < buffer.Length)
        {
            var count = await http
                .Request.Body.ReadAsync(buffer.AsMemory(read), ct)
                .ConfigureAwait(false);

            if (count == 0)
                break;

            read += count;
        }

        if (read > limit)
            return (null, true);

        return (read == 0 ? null : Encoding.UTF8.GetString(buffer, 0, read), false);
    }

    private static string Error(string code) => new JsonObject { ["error"] = code }.ToJsonString();

    private static Task TooManyRequestsAsync(HttpContext http, TimeSpan retryAfter)
    {
        http.Response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
            .ToString(CultureInfo.InvariantCulture);

        return WriteAsync(
            http,
            WiredWebApiErrors.StatusOf(WiredWebApiErrors.TOO_MANY_REQUESTS),
            Error(WiredWebApiErrors.TOO_MANY_REQUESTS)
        );
    }

    private static async Task WriteAsync(HttpContext http, int status, string? json)
    {
        http.Response.StatusCode = status;

        if (json is null)
            return;

        http.Response.ContentType = "application/json";

        await http.Response.WriteAsync(json, http.RequestAborted).ConfigureAwait(false);
    }
}

/// <summary>
/// Each room's limits per kind of call (<see cref="WiredApiConfig"/>): a call needs room in both
/// the 10-second burst window and the 60-second sustained one. Only calls whose keys open the
/// room count against it, so callers without them cannot use up its owner's allowance; their
/// refused calls count against their own address instead (<see cref="WiredApiConfig.Failures"/>).
/// The windows run on the server's <see cref="TimeProvider"/>, each from the first call it counts.
/// </summary>
internal sealed class WiredApiRateLimits(WiredApiConfig config, TimeProvider timeProvider)
{
    private readonly FixedWindows<int> _rooms = new(timeProvider);

    private readonly FixedWindows<string> _failures = new(timeProvider);

    /// <summary>Null when the room has room for the call, else how long until it has.</summary>
    public TimeSpan? TryAcquire(int roomId, WiredWebApiOperation operation) =>
        _rooms.TryAcquire(roomId, LimitOf(operation), 1);

    /// <summary>Null while <paramref name="caller"/> may still be refused, else how long until it may.</summary>
    public TimeSpan? FailureRetryAfter(string caller) =>
        _failures.TryAcquire(caller, config.Failures, 0);

    public void CountFailure(string caller) => _failures.TryAcquire(caller, config.Failures, 1);

    private WiredApiLimit LimitOf(WiredWebApiOperation operation) =>
        operation switch
        {
            WiredWebApiOperation.ListValues => config.Lists,
            WiredWebApiOperation.FindUserProfile
            or WiredWebApiOperation.GetUserProfile
            or WiredWebApiOperation.GetFurniProfile
            or WiredWebApiOperation.GetGlobalProfile => config.ProfileReads,
            WiredWebApiOperation.PutValue
            or WiredWebApiOperation.PatchValue
            or WiredWebApiOperation.DeleteValue
            or WiredWebApiOperation.PatchGlobal
            or WiredWebApiOperation.DeleteUserProfile => config.Writes,
            WiredWebApiOperation.BulkDelete => config.BulkDeletes,
            WiredWebApiOperation.Batch
            or WiredWebApiOperation.PatchUserProfile
            or WiredWebApiOperation.PatchFurniProfile
            or WiredWebApiOperation.PatchGlobalProfile => config.Batches,
            _ => config.SimpleReads,
        };

    /// <summary>
    /// The burst and sustained windows of each key: a call is counted in both or in neither.
    /// Keys whose windows have all ended are dropped once a minute.
    /// </summary>
    private sealed class FixedWindows<TKey>
        where TKey : notnull
    {
        private static readonly TimeSpan BURST = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan SUSTAINED = TimeSpan.FromMinutes(1);

        private readonly TimeProvider _timeProvider;
        private readonly Dictionary<(TKey, WiredApiLimit), Window[]> _windows = [];
        private long _sweptAt;

        public FixedWindows(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
            _sweptAt = timeProvider.GetTimestamp();
        }

        /// <summary>Counts <paramref name="permits"/> (0 only asks), else how long until there is room.</summary>
        public TimeSpan? TryAcquire(TKey key, WiredApiLimit limit, int permits)
        {
            var now = _timeProvider.GetTimestamp();

            lock (_windows)
            {
                Sweep(now);

                if (!_windows.TryGetValue((key, limit), out var windows))
                {
                    if (permits == 0)
                        return null;

                    windows =
                    [
                        new Window(BURST, limit.Burst, now),
                        new Window(SUSTAINED, limit.PerMinute, now),
                    ];
                    _windows[(key, limit)] = windows;
                }

                TimeSpan? retryAfter = null;

                foreach (var window in windows)
                {
                    if (window.HasEnded(now, _timeProvider))
                        window.Restart(now);

                    if (window.IsFull(permits) && !(retryAfter >= window.Left(now, _timeProvider)))
                        retryAfter = window.Left(now, _timeProvider);
                }

                if (retryAfter is not null)
                    return retryAfter;

                foreach (var window in windows)
                    window.Used += permits;

                return null;
            }
        }

        private void Sweep(long now)
        {
            if (_timeProvider.GetElapsedTime(_sweptAt, now) < SUSTAINED)
                return;

            _sweptAt = now;

            foreach (var (key, windows) in _windows)
            {
                if (windows.All(x => x.HasEnded(now, _timeProvider)))
                    _windows.Remove(key);
            }
        }
    }

    private sealed class Window(TimeSpan length, int permits, long startedAt)
    {
        private readonly int _permits = Math.Max(1, permits);
        private long _startedAt = startedAt;

        public int Used { get; set; }

        public bool IsFull(int permits) =>
            permits == 0 ? Used >= _permits : Used + permits > _permits;

        public bool HasEnded(long now, TimeProvider timeProvider) =>
            timeProvider.GetElapsedTime(_startedAt, now) >= length;

        public TimeSpan Left(long now, TimeProvider timeProvider) =>
            length - timeProvider.GetElapsedTime(_startedAt, now);

        public void Restart(long now)
        {
            _startedAt = now;
            Used = 0;
        }
    }
}
