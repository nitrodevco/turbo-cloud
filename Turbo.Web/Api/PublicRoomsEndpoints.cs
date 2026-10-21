using System;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Web.Api.Contracts;
using Turbo.Web.Configuration;

namespace Turbo.Web.Api;

/// <summary>
/// The public API's rooms (Habbo's OpenAPI document): <c>/public/rooms/{roomId}</c>, "a public
/// room", and <c>/public/users/{id}/rooms</c>, the public rooms a player owns, "only returned if
/// the user is found and the profile is visible". A room shows here when the navigator lists it to
/// anyone: not invisible and not hidden by Builders Club (inference). The errors are those of an
/// official client capture of these endpoints (2026-10-16): <c>{"error":"not-found"}</c> for no
/// such room, player or a hidden profile, and the <c>errors</c> list for an id that cannot be one.
/// </summary>
internal sealed class PublicRoomsEndpoints(
    IDbContextFactory<TurboDbContext> dbContextFactory,
    IOptions<WebConfig> config
)
{
    private PublicApiConfig Api => config.Value.PublicApi;

    public void Map(IEndpointRouteBuilder api)
    {
        if (!Api.Enabled)
            return;

        api.MapGet("/public/rooms/{roomId:int}", RoomAsync);
        api.MapGet("/public/users/{id}/rooms", PlayerRoomsAsync);
    }

    // Plain GETs without headers of their own, so a browser asks no preflight; it only needs
    // leave to read the answer.
    private async Task<IResult> RoomAsync(HttpContext http, int roomId, CancellationToken ct)
    {
        ApiCors.AllowOrigin(http, Api.AllowedOrigins);

        var rooms = await PublicRoomsAsync(room => room.Id == roomId, ct).ConfigureAwait(false);

        return rooms.Length == 0 ? NotFound() : Results.Ok(rooms[0]);
    }

    private static IResult NotFound() => Results.Json(new { error = "not-found" }, statusCode: 404);

    /// <summary>The player's rooms, oldest first; 404 for no such player or a hidden profile, not an empty list.</summary>
    private async Task<IResult> PlayerRoomsAsync(HttpContext http, string id, CancellationToken ct)
    {
        ApiCors.AllowOrigin(http, Api.AllowedOrigins);

        if (!int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var playerId))
            return Results.Json(
                new
                {
                    errors = new[]
                    {
                        new
                        {
                            param = "id",
                            msg = "user.invalid_id",
                            value = id,
                        },
                    },
                },
                statusCode: 404
            );

        var db = await dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        await using (db.ConfigureAwait(false))
        {
            var visible =
                await db.Players.AnyAsync(x => x.Id == playerId, ct).ConfigureAwait(false)
                && !await db
                    .PlayerSettings.AnyAsync(
                        x => x.PlayerEntityId == playerId && x.ProfileHidden,
                        ct
                    )
                    .ConfigureAwait(false);

            if (!visible)
                return NotFound();
        }

        return Results.Ok(
            await PublicRoomsAsync(room => room.PlayerEntityId == playerId, ct)
                .ConfigureAwait(false)
        );
    }

    private async Task<PublicApiRoom[]> PublicRoomsAsync(
        Expression<Func<RoomEntity, bool>> which,
        CancellationToken ct
    )
    {
        var db = await dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var rows = await db
            .Rooms.AsNoTracking()
            .Where(which)
            .Where(room => room.DoorMode != RoomDoorModeType.Invisible && !room.HiddenByBc)
            .OrderBy(room => room.Id)
            .Select(room => new
            {
                room.Id,
                room.Name,
                room.Description,
                room.CreatedAt,
                room.Tags,
                room.PlayersMax,
                room.HideOwner,
                OwnerId = room.PlayerEntityId,
                OwnerName = room.PlayerEntity.Name,
                CategoryName = room.NavigatorFlatCategoryEntity == null
                    ? null
                    : room.NavigatorFlatCategoryEntity.Name,
                CategoryKey = room.NavigatorFlatCategoryEntity == null
                    ? null
                    : room.NavigatorFlatCategoryEntity.GlobalCategory,
                room.Score,
                room.StaffPick,
                room.DoorMode,
                GuildId = db
                    .Guilds.Where(guild => guild.RoomEntityId == room.Id)
                    .Select(guild => (int?)guild.Id)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new PublicApiRoom(
                row.Id,
                row.Name,
                row.Description ?? string.Empty,
                // As the document's example and the capture: to the second, 2026-10-12T06:20:23.000+00:00.
                DateTime
                    .SpecifyKind(row.CreatedAt, DateTimeKind.Utc)
                    .ToString("yyyy-MM-dd'T'HH:mm:ss'.000+00:00'", CultureInfo.InvariantCulture),
                row.GuildId?.ToString(CultureInfo.InvariantCulture),
                [.. RoomTags.Parse(row.Tags)],
                row.PlayersMax,
                !row.HideOwner,
                row.HideOwner ? null : row.OwnerName,
                row.HideOwner ? null : row.OwnerId.ToString(CultureInfo.InvariantCulture),
                CategoryOf(row.CategoryKey, row.CategoryName),
                UrlOf(Api.RoomThumbnailUrl, row.Id),
                UrlOf(Api.RoomImageUrl, row.Id),
                row.Score,
                // The navigator's official rooms (inference: the capture's own room says false).
                row.StaffPick,
                DoorModeOf(row.DoorMode),
                row.Id.ToString(CultureInfo.InvariantCulture)
            )),
        ];
    }

    /// <summary>
    /// The category's text key, as Flash names it (<c>FlatCategory.visibleName</c>): its global
    /// key's <c>navigator.flatcategory.global.*</c>, else its own name.
    /// </summary>
    private static string[] CategoryOf(string? globalKey, string? name) =>
        !string.IsNullOrEmpty(globalKey) ? [$"navigator.flatcategory.global.{globalKey}"]
        : !string.IsNullOrEmpty(name) ? [name]
        : [];

    /// <summary>
    /// The capture's <c>"open"</c>; the others by the hotel's text keys
    /// (<c>navigator.roomsettings.doormode.doorbell|password|invisible</c>) and Flash's
    /// <c>RoomSettingsData.DOOR_MODE_NOOBS_ONLY</c> (inference: only "open" is recorded).
    /// </summary>
    private static string DoorModeOf(RoomDoorModeType doorMode) =>
        doorMode switch
        {
            RoomDoorModeType.Locked => "doorbell",
            RoomDoorModeType.Password => "password",
            RoomDoorModeType.Invisible => "invisible",
            RoomDoorModeType.NoobLobby => "noobs_only",
            _ => "open",
        };

    private static string? UrlOf(string template, int roomId) =>
        string.IsNullOrEmpty(template)
            ? null
            : template.Replace(
                "{roomId}",
                roomId.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal
            );
}
