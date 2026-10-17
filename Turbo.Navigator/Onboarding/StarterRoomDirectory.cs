using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Turbo.Database.Context;
using Turbo.Database.Entities.Navigator;
using Turbo.Players.Configuration;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Settings;

namespace Turbo.Navigator.Onboarding;

/// <summary>
/// <see cref="IStarterRoomDirectory"/> over <c>starter_rooms</c>. After every change the client's
/// side is written again: <c>new.user.flow.roomTypes</c> lists the rooms' ids in picker order,
/// <c>new.user.flow.room.image.url</c> points at the gamedata host's
/// <c>/gamedata/starter-rooms/%type%.png</c>, and each room's <c>room.name.&lt;id&gt;</c> and
/// <c>room.description.&lt;id&gt;</c> are its own name and description. With no rooms left the
/// built-in ones are offered again: room type <c>10</c> and Habbo's picture.
/// </summary>
public sealed class StarterRoomDirectory(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGamedataVariableService variables,
    IGamedataTextService texts,
    IServerSettings settings,
    IOptions<OnboardingConfig> config,
    ILogger<IStarterRoomDirectory> logger
) : IStarterRoomDirectory
{
    public const string ROOM_TYPES_VARIABLE = "new.user.flow.roomTypes";
    public const string IMAGE_URL_VARIABLE = "new.user.flow.room.image.url";

    /// <summary>What the client offers with no rooms of the hotel's: the built-in room and Habbo's picture.</summary>
    private const string BUILT_IN_ROOM_TYPES = "10";
    private const string BUILT_IN_IMAGE_URL =
        "https://images.habbo.com/c_images/nux/nux_room_%type%_round.png";

    /// <summary>Where the gamedata host serves the pictures, under its public address.</summary>
    public const string PICTURE_ROUTE = "/gamedata/starter-rooms/";

    private const string GAMEDATA_PUBLIC_URL_SETTING = "Turbo:Gamedata:PublicUrl";

    public int Capacity => config.Value.MaxStarterRooms;

    public async Task<ImmutableArray<StarterRoomSnapshot>> ListAsync(CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var rows = await db
            .StarterRooms.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Id)
            .Select(x => new StarterRoomSnapshot(
                RoomId.Parse(x.RoomEntityId),
                x.RoomEntity.Name,
                x.RoomEntity.Description ?? string.Empty,
                x.SortOrder,
                x.Name,
                x.Description,
                x.PictureTakenAt
            ))
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return [.. rows];
    }

    public async Task<StarterRoomSnapshot?> GetAsync(RoomId roomId, CancellationToken ct) =>
        (await ListAsync(ct).ConfigureAwait(false)).FirstOrDefault(x => x.RoomId == roomId);

    public async Task<StarterRoomChange> AddAsync(RoomId roomId, PlayerId by, CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        if (!await db.Rooms.AnyAsync(x => x.Id == roomId.Value, ct).ConfigureAwait(false))
            return StarterRoomChange.RoomNotFound;

        if (
            await db
                .StarterRooms.AnyAsync(x => x.RoomEntityId == roomId.Value, ct)
                .ConfigureAwait(false)
        )
            return StarterRoomChange.AlreadyAdded;

        var count = await db.StarterRooms.CountAsync(ct).ConfigureAwait(false);

        if (count >= Capacity)
            return StarterRoomChange.Full;

        var last = await db
            .StarterRooms.Select(x => (int?)x.SortOrder)
            .MaxAsync(ct)
            .ConfigureAwait(false);

        db.StarterRooms.Add(
            new StarterRoomEntity
            {
                RoomEntityId = roomId.Value,
                SortOrder = (last ?? 0) + 1,
                CreatedByPlayerId = by.Value > 0 ? by.Value : null,
            }
        );

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // Added meanwhile: the unique room id says so.
            return StarterRoomChange.AlreadyAdded;
        }

        logger.LogInformation(
            "Player {PlayerId} offered room {RoomId} as a starter room",
            by,
            roomId
        );

        await SyncClientAsync(by, ct).ConfigureAwait(false);

        return StarterRoomChange.Added;
    }

    public async Task<StarterRoomChange> RemoveAsync(
        RoomId roomId,
        PlayerId by,
        CancellationToken ct
    )
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var removed = await db
            .StarterRooms.Where(x => x.RoomEntityId == roomId.Value)
            .ExecuteDeleteAsync(ct)
            .ConfigureAwait(false);

        if (removed == 0)
            return StarterRoomChange.NotAStarterRoom;

        logger.LogInformation(
            "Player {PlayerId} stopped offering room {RoomId} as a starter room",
            by,
            roomId
        );

        await SyncClientAsync(by, ct).ConfigureAwait(false);

        return StarterRoomChange.Removed;
    }

    public StarterRoomPicturePaths GetPicturePaths(RoomId roomId)
    {
        var folder = config.Value.PictureDirectory;

        Directory.CreateDirectory(folder);

        return new(
            Path.Combine(folder, $"{roomId.Value}.json"),
            Path.Combine(folder, $"{roomId.Value}.scene.png"),
            Path.Combine(folder, $"{roomId.Value}.png")
        );
    }

    public string? GetPictureFile(RoomId roomId)
    {
        var path = Path.Combine(config.Value.PictureDirectory, $"{roomId.Value}.png");

        return File.Exists(path) ? path : null;
    }

    public async Task<bool> PictureTakenAsync(RoomId roomId, PlayerId by, CancellationToken ct)
    {
        var db = await dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var updated = await db
            .StarterRooms.Where(x => x.RoomEntityId == roomId.Value)
            .ExecuteUpdateAsync(up => up.SetProperty(x => x.PictureTakenAt, DateTime.UtcNow), ct)
            .ConfigureAwait(false);

        if (updated == 0)
            return false;

        await SyncClientAsync(by, ct).ConfigureAwait(false);

        return true;
    }

    /// <summary>Writes the client's room types, picture address and room texts from the rooms offered.</summary>
    private async Task SyncClientAsync(PlayerId by, CancellationToken ct)
    {
        var rooms = await ListAsync(ct).ConfigureAwait(false);

        if (rooms.IsEmpty)
        {
            await SetVariableAsync(ROOM_TYPES_VARIABLE, BUILT_IN_ROOM_TYPES, by, ct)
                .ConfigureAwait(false);
            await SetVariableAsync(IMAGE_URL_VARIABLE, BUILT_IN_IMAGE_URL, by, ct)
                .ConfigureAwait(false);

            return;
        }

        await SetVariableAsync(
                ROOM_TYPES_VARIABLE,
                string.Join(',', rooms.Select(x => x.RoomId.Value)),
                by,
                ct
            )
            .ConfigureAwait(false);

        if (PublicUrl() is { } publicUrl)
            await SetVariableAsync(
                    IMAGE_URL_VARIABLE,
                    $"{publicUrl}{PICTURE_ROUTE}%type%.png",
                    by,
                    ct
                )
                .ConfigureAwait(false);
        else
            logger.LogWarning(
                "Starter room pictures need {Setting} (the gamedata host's public address) to be shown",
                GAMEDATA_PUBLIC_URL_SETTING
            );

        foreach (var room in rooms)
        {
            await texts
                .SaveAsync($"room.name.{room.RoomId.Value}", room.RoomName, by, ct)
                .ConfigureAwait(false);
            await texts
                .SaveAsync($"room.description.{room.RoomId.Value}", room.RoomDescription, by, ct)
                .ConfigureAwait(false);
        }
    }

    private Task SetVariableAsync(string key, string value, PlayerId by, CancellationToken ct) =>
        variables.SaveAsync(key, JsonSerializer.Serialize(value), by, ct);

    /// <summary>The gamedata host's public address, without a trailing slash; null when it has none.</summary>
    private string? PublicUrl()
    {
        var json = settings.GetValue(GAMEDATA_PUBLIC_URL_SETTING);

        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            var url = JsonSerializer.Deserialize<string>(json);

            return string.IsNullOrWhiteSpace(url) ? null : url.TrimEnd('/');
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
