using System;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Database.Extensions;

/// <summary>
/// Row to snapshot, shared by the navigator (listings) and the room grain (the live room).
/// Population and ranking are runtime values and always start at zero.
/// </summary>
public static class RoomEntityExtensions
{
    public static RoomInfoSnapshot ToInfoSnapshot(
        this RoomEntity entity,
        string ownerName,
        RoomEventEntity? activeEvent,
        DateTime nowUtc
    ) =>
        new()
        {
            RoomId = entity.Id,
            Name = entity.Name ?? string.Empty,
            Description = entity.Description ?? string.Empty,
            OwnerId = PlayerId.Parse(entity.PlayerEntityId),
            OwnerName = ownerName,
            Population = 0,
            DoorMode = entity.DoorMode,
            PlayersMax = entity.PlayersMax,
            TradeType = entity.TradeType,
            Score = entity.Score,
            Ranking = 0,
            CategoryId = entity.NavigatorCategoryEntityId ?? -1,
            Tags = RoomTags.Parse(entity.Tags),
            AllowBlocking = entity.AllowBlocking,
            AllowPets = entity.AllowPets,
            AllowPetsEat = entity.AllowPetsEat,
            StaffPick = entity.StaffPick,
            ActiveEvent = activeEvent?.ToSnapshot(ownerName),
            HiddenByBc = entity.HiddenByBc,
            LastUpdatedUtc = nowUtc,
        };

    /// <summary>
    /// The live room: the listing fields come from <see cref="ToInfoSnapshot"/>, so they are
    /// mapped in one place, and only the room-only fields are added here.
    /// </summary>
    /// <param name="worldType">The name of the room model the room is built on.</param>
    public static RoomSnapshot ToSnapshot(
        this RoomEntity entity,
        string ownerName,
        string worldType,
        RoomEventEntity? activeEvent,
        DateTime nowUtc
    ) =>
        new(entity.ToInfoSnapshot(ownerName, activeEvent, nowUtc))
        {
            Password = entity.Password ?? string.Empty,
            ModSettings = new ModSettingsSnapshot
            {
                WhoCanMute = entity.MuteType,
                WhoCanKick = entity.KickType,
                WhoCanBan = entity.BanType,
            },
            ChatProtection = entity.ChatFloodType,
            WorldType = worldType,
            HideWalls = entity.HideWalls,
            WallThickness = entity.ThicknessWall,
            FloorThickness = entity.ThicknessFloor,
            LeaveOnDoorTile = entity.LeaveOnDoorTile,
            IdleSleepEnabled = entity.IdleSleepEnabled,
            IdleSleepTimeoutSeconds = entity.IdleSleepTimeoutSeconds,
            IdleAutokickEnabled = entity.IdleAutokickEnabled,
            IdleAutokickTimeoutSeconds = entity.IdleAutokickTimeoutSeconds,
            MuteAllPets = entity.MuteAllPets,
            WallHeight = entity.WallHeight,
            WiredModifyPermissionMask = entity.WiredModifyPermissionMask,
            WiredReadPermissionMask = entity.WiredReadPermissionMask,
            WiredTimezone = entity.WiredTimezone,
        };

    /// <param name="ownerName">The room owner's name, which the client shows as the host.</param>
    public static RoomEventSnapshot ToSnapshot(this RoomEventEntity entity, string ownerName) =>
        new()
        {
            EventId = entity.Id,
            RoomId = entity.RoomEntityId,
            OwnerId = PlayerId.Parse(entity.PlayerEntityId),
            OwnerName = ownerName,
            CategoryId = entity.NavigatorEventCategoryEntityId,
            Name = entity.Name,
            Description = entity.Description,
            CreatedAtUtc = entity.CreatedAt,
            ExpiresAtUtc = entity.ExpiresAt,
        };
}
