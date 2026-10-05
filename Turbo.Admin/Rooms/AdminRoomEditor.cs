using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Orleans;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Primitives.Action;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Admin.Rooms;

/// <summary>
/// Saving a room's settings from the panel, through the one path the owner's settings dialog
/// takes: the room grain's <c>SaveRoomSettingsAsync</c>, as the staff member, so the room checks
/// they own it or hold <c>room.control.any</c>, validates, saves, refreshes the navigator and tells
/// everyone inside. Two things the game's packet handler does first are done here instead: the
/// category is checked to exist (staff may put a room in any category, staff-only ones included),
/// and a password door keeps its password when none is typed, since the panel never sees it.
/// </summary>
public sealed class AdminRoomEditor(
    IDbContextFactory<TurboDbContext> database,
    IGrainFactory grainFactory
)
{
    public async Task<AdminRoomSaveResult> SaveSettingsAsync(
        PlayerId editor,
        int roomId,
        RoomSettingsRequest request,
        CancellationToken ct
    )
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var saved = await db
            .Rooms.AsNoTracking()
            .Where(x => x.Id == roomId)
            .Select(x => new { x.Password })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (saved is null)
            return new AdminRoomSaveResult(
                AdminRoomSaveOutcome.NotFound,
                $"There is no room {roomId}."
            );

        if (
            !TryParse<RoomDoorModeType>(request.DoorMode, out var doorMode)
            || !TryParse<RoomTradeModeType>(request.TradeMode, out var tradeMode)
            || !TryParse<RoomThicknessType>(request.WallThickness, out var wallThickness)
            || !TryParse<RoomThicknessType>(request.FloorThickness, out var floorThickness)
            || !TryParse<ModSettingType>(request.WhoCanMute, out var whoCanMute)
            || !TryParse<ModSettingType>(request.WhoCanKick, out var whoCanKick)
            || !TryParse<ModSettingType>(request.WhoCanBan, out var whoCanBan)
            || !TryParse<ChatFloodSensitivityType>(request.ChatFloodProtection, out var chatFlood)
        )
            return AdminRoomSaveResult.Invalid(
                "One of the settings is not a value the room knows. Reload the page and try again."
            );

        if (
            request.CategoryId is > 0 and var categoryId
            && !await db
                .NavigatorFlatCategories.AnyAsync(x => x.Id == categoryId, ct)
                .ConfigureAwait(false)
        )
            return AdminRoomSaveResult.Invalid($"There is no category {categoryId}.");

        var password =
            doorMode == RoomDoorModeType.Password && string.IsNullOrWhiteSpace(request.Password)
                ? saved.Password ?? string.Empty
                : request.Password ?? string.Empty;

        var result = await grainFactory
            .GetRoomGrain(new RoomId(roomId))
            .SaveRoomSettingsAsync(
                ActionContext.CreateForPlayer(editor, new RoomId(roomId)),
                new RoomSettingsUpdateSnapshot
                {
                    Name = request.Name ?? string.Empty,
                    Description = request.Description ?? string.Empty,
                    DoorMode = (int)doorMode,
                    Password = password,
                    MaximumVisitors = request.MaxPlayers,
                    CategoryId = request.CategoryId,
                    Tags = [.. request.Tags ?? []],
                    TradeMode = tradeMode,
                    AllowPets = request.AllowPets,
                    AllowPetsEat = request.AllowPetsEat,
                    AllowWalkThrough = request.AllowWalkThrough,
                    HideWalls = request.HideWalls,
                    WallThickness = wallThickness,
                    FloorThickness = floorThickness,
                    WhoCanMute = whoCanMute,
                    WhoCanKick = whoCanKick,
                    WhoCanBan = whoCanBan,
                    ChatProtection = chatFlood,
                    LeaveOnDoorTile = request.LeaveOnDoorTile,
                    IdleSleepEnabled = request.IdleSleepEnabled,
                    IdleSleepTimeoutSeconds = request.IdleSleepTimeoutSeconds,
                    IdleAutokickEnabled = request.IdleAutokickEnabled,
                    IdleAutokickTimeoutSeconds = request.IdleAutokickTimeoutSeconds,
                    MuteAllPets = request.MuteAllPets,
                },
                ct
            )
            .ConfigureAwait(false);

        return result.Error switch
        {
            RoomSettingsSaveErrorType.None => AdminRoomSaveResult.Saved,
            RoomSettingsSaveErrorType.NameRequired => AdminRoomSaveResult.Invalid(
                "A room needs a name."
            ),
            RoomSettingsSaveErrorType.PasswordRequired => AdminRoomSaveResult.Invalid(
                "A password door needs a password."
            ),
            RoomSettingsSaveErrorType.TagTooLong => AdminRoomSaveResult.Invalid(
                $"The tag \"{result.Info}\" is too long."
            ),
            _ => AdminRoomSaveResult.Invalid(
                "The room refused those settings: you need to own it or hold room.control.any, and the password must not be too long."
            ),
        };
    }

    private static bool TryParse<T>(string? value, out T parsed)
        where T : struct, Enum =>
        Enum.TryParse(value, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);
}
