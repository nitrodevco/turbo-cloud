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
using Turbo.Primitives.Rooms.Wired.WebApi;
using Turbo.Web.Api.Contracts;
using Turbo.Web.Configuration;

namespace Turbo.Web.Api;

/// <summary>
/// The public API's rooms (Habbo's OpenAPI document): <c>/public/rooms/{roomId}</c>, "a public
/// room", and <c>/public/users/{id}/rooms</c>, the public rooms a player owns, "only returned if
/// the user is found and the profile is visible". A public room is one the navigator lists to
/// anyone: not invisible and not hidden by Builders Club.
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

    private async Task<IResult> RoomAsync(int roomId, CancellationToken ct)
    {
        var rooms = await PublicRoomsAsync(room => room.Id == roomId, ct).ConfigureAwait(false);

        // The document's room-not-found error (WiredRoomNotFoundError) is the only one it names.
        return rooms.Length == 0
            ? Results.Json(new { error = WiredWebApiErrors.ROOM_NOT_FOUND }, statusCode: 404)
            : Results.Ok(rooms[0]);
    }

    /// <summary>The player's public rooms, oldest first; 404 for no such player or a hidden profile.</summary>
    private async Task<IResult> PlayerRoomsAsync(string id, CancellationToken ct)
    {
        if (!int.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var playerId))
            return Results.NotFound();

        await using (var db = await dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false))
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
                return Results.NotFound();
        }

        return Results.Ok(
            await PublicRoomsAsync(room => room.PlayerEntityId == playerId, ct)
                .ConfigureAwait(false)
        );
    }

    private async Task<PublicRoom[]> PublicRoomsAsync(
        Expression<Func<RoomEntity, bool>> which,
        CancellationToken ct
    )
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

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
                GuildId = db
                    .Guilds.Where(guild => guild.RoomEntityId == room.Id)
                    .Select(guild => (int?)guild.Id)
                    .FirstOrDefault(),
            })
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(row => new PublicRoom(
                row.Id,
                row.Name,
                row.Description ?? string.Empty,
                // As the document's example: 2000-11-01T11:41:05.000+00:00.
                DateTime
                    .SpecifyKind(row.CreatedAt, DateTimeKind.Utc)
                    .ToString("yyyy-MM-dd'T'HH:mm:ss.fff'+00:00'", CultureInfo.InvariantCulture),
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

    private static string? UrlOf(string template, int roomId) =>
        string.IsNullOrEmpty(template)
            ? null
            : template.Replace(
                "{roomId}",
                roomId.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal
            );
}
