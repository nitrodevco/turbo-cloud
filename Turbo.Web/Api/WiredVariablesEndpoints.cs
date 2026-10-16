using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.RateLimiting;
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
    IOptions<WebConfig> config
)
{
    private const string READ_KEY_HEADER = "X-Wired-Read-Key";
    private const string WRITE_KEY_HEADER = "X-Wired-Write-Key";

    private readonly WiredApiRateLimits _limits = new(config.Value.WiredApi);

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
    }

    private RequestDelegate Handle(WiredWebApiOperation operation) =>
        http => HandleAsync(http, operation, http.RequestAborted);

    private async Task HandleAsync(
        HttpContext http,
        WiredWebApiOperation operation,
        CancellationToken ct
    )
    {
        var route = http.Request.RouteValues;
        var roomId = int.Parse((string)route["roomId"]!);

        if (!_limits.TryAcquire(roomId, operation))
        {
            await WriteAsync(
                http,
                WiredWebApiErrors.StatusOf(WiredWebApiErrors.TOO_MANY_REQUESTS),
                Error(WiredWebApiErrors.TOO_MANY_REQUESTS)
            );

            return;
        }

        if (!await RoomExistsAsync(roomId, ct))
        {
            await WriteAsync(http, 404, Error(WiredWebApiErrors.ROOM_NOT_FOUND));

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
        );
        var request = new WiredWebApiRequest
        {
            Operation = operation,
            ReadKey = Header(http, READ_KEY_HEADER),
            WriteKey = Header(http, WRITE_KEY_HEADER),
            Scope = route["scope"] as string ?? string.Empty,
            VariableName = route["variableName"] as string ?? string.Empty,
            TargetKind = route["targetKind"] as string ?? string.Empty,
            EntityId = route["entityId"] as string ?? string.Empty,
            Body = await ReadBodyAsync(http, ct),
            Page = int.TryParse(query["page"], out var page) ? page : null,
            Size = int.TryParse(query["size"], out var size) ? size : null,
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

        await WriteAsync(http, response.Status, response.Json);
    }

    private async Task<bool> RoomExistsAsync(int roomId, CancellationToken ct)
    {
        if (roomId <= 0)
            return false;

        await using var db = await dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        return await db.Rooms.AnyAsync(room => room.Id == roomId, ct).ConfigureAwait(false);
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

        await using var db = await dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

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

    private static string? Header(HttpContext http, string name) =>
        http.Request.Headers.TryGetValue(name, out var value) ? value.ToString() : null;

    private async Task<string?> ReadBodyAsync(HttpContext http, CancellationToken ct)
    {
        if (
            http.Request.ContentLength is 0
            || !(
                HttpMethods.IsPost(http.Request.Method)
                || HttpMethods.IsPut(http.Request.Method)
                || HttpMethods.IsPatch(http.Request.Method)
            )
        )
            return null;

        var buffer = new char[Api.MaxBodyBytes];
        using var reader = new StreamReader(http.Request.Body, Encoding.UTF8);
        var read = await reader.ReadBlockAsync(buffer.AsMemory(), ct).ConfigureAwait(false);

        return read == 0 ? null : new string(buffer, 0, read);
    }

    private static string Error(string code) => new JsonObject { ["error"] = code }.ToJsonString();

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
/// the 10-second burst window and the 60-second sustained one.
/// </summary>
internal sealed class WiredApiRateLimits(WiredApiConfig config)
{
    private readonly PartitionedRateLimiter<(int, WiredApiLimit)> _limiter =
        PartitionedRateLimiter.CreateChained(
            Window(TimeSpan.FromSeconds(10), limit => limit.Burst),
            Window(TimeSpan.FromMinutes(1), limit => limit.PerMinute)
        );

    public bool TryAcquire(int roomId, WiredWebApiOperation operation)
    {
        using var lease = _limiter.AttemptAcquire((roomId, LimitOf(operation)));

        return lease.IsAcquired;
    }

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

    private static PartitionedRateLimiter<(int, WiredApiLimit)> Window(
        TimeSpan window,
        Func<WiredApiLimit, int> permits
    ) =>
        PartitionedRateLimiter.Create<(int, WiredApiLimit), (int, WiredApiLimit)>(key =>
            RateLimitPartition.GetFixedWindowLimiter(
                key,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, permits(key.Item2)),
                    Window = window,
                    QueueLimit = 0,
                }
            )
        );
}
