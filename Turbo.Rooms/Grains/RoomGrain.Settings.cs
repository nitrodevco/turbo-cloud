using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Room.Chat;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<RoomSettingsSnapshot?> GetRoomSettingsAsync(
        ActionContext ctx,
        CancellationToken ct
    )
    {
        await SecurityModule.EnsureRightsLoadedAsync(ct);

        var controllerLevel = await SecurityModule.GetControllerLevelAsync(ctx);

        if (controllerLevel < RoomControllerType.Owner)
            return null;

        return new RoomSettingsSnapshot
        {
            Room = _state.RoomSnapshot,
            MaximumVisitorsLimit = _roomConfig.MaxPlayersLimit,
        };
    }

    public async Task<RoomSettingsSaveResultSnapshot> SaveRoomSettingsAsync(
        ActionContext ctx,
        RoomSettingsUpdateSnapshot settings,
        CancellationToken ct
    )
    {
        if (!await IsRoomOwnerAsync(ctx, ct))
            return RoomSettingsSaveResultSnapshot.Failed(RoomSettingsSaveErrorType.Invalid);

        var current = _state.RoomSnapshot;
        var name = Truncate(settings.Name, _roomConfig.RoomNameMaxLength);

        if (name.Length == 0)
            return RoomSettingsSaveResultSnapshot.Failed(RoomSettingsSaveErrorType.NameRequired);

        var description = Truncate(settings.Description, _roomConfig.RoomDescriptionMaxLength);

        if (!Enum.IsDefined((RoomDoorModeType)settings.DoorMode))
            return Rejected(ctx, "door mode", settings.DoorMode);

        var doorMode = (RoomDoorModeType)settings.DoorMode;
        var password = settings.Password?.Trim() ?? string.Empty;

        if (doorMode == RoomDoorModeType.Password)
        {
            if (password.Length == 0)
                return RoomSettingsSaveResultSnapshot.Failed(
                    RoomSettingsSaveErrorType.PasswordRequired
                );

            if (password.Length > _roomConfig.RoomPasswordMaxLength)
                return RoomSettingsSaveResultSnapshot.Failed(RoomSettingsSaveErrorType.Invalid);
        }
        else
        {
            // Only a password door keeps a password.
            password = string.Empty;
        }

        if (
            !Enum.IsDefined(settings.TradeMode)
            || !Enum.IsDefined(settings.WallThickness)
            || !Enum.IsDefined(settings.FloorThickness)
            || !Enum.IsDefined(settings.WhoCanMute)
            || !Enum.IsDefined(settings.WhoCanKick)
            || !Enum.IsDefined(settings.WhoCanBan)
            || !Enum.IsDefined(settings.ChatProtection)
        )
            return Rejected(ctx, "settings enum", 0);

        var overlongTag = settings.Tags.FirstOrDefault(x =>
            (x?.Trim().Length ?? 0) > _roomConfig.RoomTagMaxLength
        );

        if (overlongTag is not null)
            return RoomSettingsSaveResultSnapshot.Failed(
                RoomSettingsSaveErrorType.TagTooLong,
                overlongTag.Trim().ToLowerInvariant()
            );

        var tags = RoomTags.Normalize(
            settings.Tags,
            _roomConfig.RoomTagsMax,
            _roomConfig.RoomTagMaxLength
        );
        var maximumVisitors = Math.Clamp(
            settings.MaximumVisitors,
            1,
            Math.Max(1, _roomConfig.MaxPlayersLimit)
        );
        var categoryId = settings.CategoryId is > 0 ? settings.CategoryId.Value : -1;
        var idleSleepTimeout = ClampTimeout(
            settings.IdleSleepTimeoutSeconds,
            settings.IdleSleepEnabled,
            _roomConfig.RoomIdleSleepTimeoutMinSeconds,
            _roomConfig.RoomIdleSleepTimeoutMaxSeconds
        );
        var idleAutokickTimeout = ClampTimeout(
            settings.IdleAutokickTimeoutSeconds,
            settings.IdleAutokickEnabled,
            _roomConfig.RoomIdleAutokickTimeoutMinSeconds,
            _roomConfig.RoomIdleAutokickTimeoutMaxSeconds
        );

        var next = current with
        {
            Name = name,
            Description = description,
            DoorMode = doorMode,
            Password = password,
            PlayersMax = maximumVisitors,
            CategoryId = categoryId,
            Tags = tags,
            TradeType = settings.TradeMode,
            AllowPets = settings.AllowPets,
            AllowPetsEat = settings.AllowPetsEat,
            AllowBlocking = settings.AllowWalkThrough,
            HideWalls = settings.HideWalls,
            WallThickness = settings.WallThickness,
            FloorThickness = settings.FloorThickness,
            ModSettings = new ModSettingsSnapshot
            {
                WhoCanMute = settings.WhoCanMute,
                WhoCanKick = settings.WhoCanKick,
                WhoCanBan = settings.WhoCanBan,
            },
            ChatProtection = settings.ChatProtection,
            LeaveOnDoorTile = settings.LeaveOnDoorTile,
            IdleSleepEnabled = settings.IdleSleepEnabled,
            IdleSleepTimeoutSeconds = idleSleepTimeout,
            IdleAutokickEnabled = settings.IdleAutokickEnabled,
            IdleAutokickTimeoutSeconds = idleAutokickTimeout,
            MuteAllPets = settings.MuteAllPets,
        };

        if (!await PersistSettingsAsync(next, ct))
            return RoomSettingsSaveResultSnapshot.Failed(RoomSettingsSaveErrorType.Invalid);

        await ApplySettingsAsync(current, next, ct);

        return RoomSettingsSaveResultSnapshot.Success;
    }

    public async Task<RoomSettingsSaveResultSnapshot> UpdateCategoryAndTradeSettingsAsync(
        ActionContext ctx,
        int? categoryId,
        RoomTradeModeType tradeMode,
        CancellationToken ct
    )
    {
        if (!await IsRoomOwnerAsync(ctx, ct))
            return RoomSettingsSaveResultSnapshot.Failed(RoomSettingsSaveErrorType.Invalid);

        if (!Enum.IsDefined(tradeMode))
            return Rejected(ctx, "trade mode", (int)tradeMode);

        var current = _state.RoomSnapshot;
        var next = current with
        {
            CategoryId = categoryId is > 0 ? categoryId.Value : -1,
            TradeType = tradeMode,
        };

        if (!await PersistSettingsAsync(next, ct))
            return RoomSettingsSaveResultSnapshot.Failed(RoomSettingsSaveErrorType.Invalid);

        await ApplySettingsAsync(current, next, ct);

        return RoomSettingsSaveResultSnapshot.Success;
    }

    private async Task<bool> IsRoomOwnerAsync(ActionContext ctx, CancellationToken ct)
    {
        await SecurityModule.EnsureRightsLoadedAsync(ct);

        if (await SecurityModule.GetControllerLevelAsync(ctx) >= RoomControllerType.Owner)
            return true;

        _logger.LogWarning(
            "Player {PlayerId} tried to change settings of room {RoomId} without owning it",
            ctx.PlayerId,
            _state.RoomId
        );

        return false;
    }

    private async Task<bool> PersistSettingsAsync(RoomSnapshot next, CancellationToken ct)
    {
        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var entity = await dbCtx.Rooms.FirstOrDefaultAsync(
                x => x.Id == _state.RoomId.Value,
                ct
            );

            if (entity is null)
                return false;

            entity.Name = next.Name;
            entity.Description = next.Description;
            entity.DoorMode = next.DoorMode;
            entity.Password = next.Password.Length == 0 ? null : next.Password;
            entity.PlayersMax = next.PlayersMax;
            entity.NavigatorCategoryEntityId = next.CategoryId > 0 ? next.CategoryId : null;
            entity.Tags = next.Tags.IsDefaultOrEmpty ? null : RoomTags.Join(next.Tags);
            entity.TradeType = next.TradeType;
            entity.AllowPets = next.AllowPets;
            entity.AllowPetsEat = next.AllowPetsEat;
            entity.AllowBlocking = next.AllowBlocking;
            entity.HideWalls = next.HideWalls;
            entity.ThicknessWall = next.WallThickness;
            entity.ThicknessFloor = next.FloorThickness;
            entity.MuteType = next.ModSettings.WhoCanMute;
            entity.KickType = next.ModSettings.WhoCanKick;
            entity.BanType = next.ModSettings.WhoCanBan;
            entity.ChatFloodType = next.ChatProtection;
            entity.LeaveOnDoorTile = next.LeaveOnDoorTile;
            entity.IdleSleepEnabled = next.IdleSleepEnabled;
            entity.IdleSleepTimeoutSeconds = next.IdleSleepTimeoutSeconds;
            entity.IdleAutokickEnabled = next.IdleAutokickEnabled;
            entity.IdleAutokickTimeoutSeconds = next.IdleAutokickTimeoutSeconds;
            entity.MuteAllPets = next.MuteAllPets;

            await dbCtx.SaveChangesAsync(ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings for room {RoomId}", _state.RoomId);

            return false;
        }
    }

    /// <summary>
    /// Publishes saved settings: the room hears about the changes it can see, the navigator
    /// directory gets the new listing data, and cached listings that no longer match are dropped.
    /// </summary>
    private async Task ApplySettingsAsync(
        RoomSnapshot current,
        RoomSnapshot next,
        CancellationToken ct
    )
    {
        _state.RoomSnapshot = next;

        await PublishToDirectoryAsync(ct);

        var changedKeys = new HashSet<string>(StringComparer.Ordinal);

        if (current.CategoryId != next.CategoryId)
        {
            changedKeys.Add(NavigatorListingKeys.Category(current.CategoryId));
            changedKeys.Add(NavigatorListingKeys.Category(next.CategoryId));
        }

        if (current.Name != next.Name || !current.Tags.SequenceEqual(next.Tags))
            changedKeys.Add(NavigatorListingKeys.SEARCH);

        if (!current.Tags.SequenceEqual(next.Tags))
            changedKeys.Add(NavigatorListingKeys.TAGS);

        if (changedKeys.Count > 0)
            await _grainFactory
                .GetRoomDirectoryGrain()
                .PublishListingChangesAsync([.. changedKeys], ct);

        if (current.ChatProtection != next.ChatProtection)
            await SendComposerToRoomAsync(
                new RoomChatSettingsMessageComposer { ChatProtection = next.ChatProtection },
                ct
            );

        if (
            current.HideWalls != next.HideWalls
            || current.WallThickness != next.WallThickness
            || current.FloorThickness != next.FloorThickness
        )
            await SendComposerToRoomAsync(
                new RoomVisualizationSettingsMessageComposer
                {
                    WallsHidden = next.HideWalls,
                    WallThickness = next.WallThickness,
                    FloorThickness = next.FloorThickness,
                },
                ct
            );

        await SendComposerToRoomAsync(
            new RoomInfoUpdatedMessageComposer { RoomId = _state.RoomId },
            ct
        );
    }

    private RoomSettingsSaveResultSnapshot Rejected(ActionContext ctx, string field, int value)
    {
        _logger.LogWarning(
            "Rejected room {RoomId} settings from player {PlayerId}: invalid {Field} {Value}",
            _state.RoomId,
            ctx.PlayerId,
            field,
            value
        );

        return RoomSettingsSaveResultSnapshot.Failed(RoomSettingsSaveErrorType.Invalid);
    }

    private static string Truncate(string? value, int maxLength)
    {
        value = value?.Trim() ?? string.Empty;

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static int ClampTimeout(int seconds, bool enabled, int min, int max) =>
        enabled ? Math.Clamp(seconds, min, max) : Math.Max(0, seconds);
}
