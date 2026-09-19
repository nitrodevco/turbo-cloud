using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains.Systems;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<WiredRoomSettingsSnapshot?> GetWiredRoomSettingsAsync(
        ActionContext ctx,
        CancellationToken ct
    )
    {
        if (!await CanReadWiredAsync(ctx, ct))
            return null;

        return CreateWiredRoomSettingsSnapshot();
    }

    public async Task<bool> SetWiredRoomSettingsAsync(
        ActionContext ctx,
        WiredPermissionFlags modifyPermissionMask,
        WiredPermissionFlags readPermissionMask,
        string timezone,
        CancellationToken ct
    )
    {
        try
        {
            await SecurityModule.EnsureRightsLoadedAsync(ct);

            if (await SecurityModule.GetControllerLevelAsync(ctx) < RoomControllerType.Owner)
                return false;

            // Modify has no "everyone" option in the client; reject anything outside the flags.
            const WiredPermissionFlags allFlags =
                WiredPermissionFlags.Everyone
                | WiredPermissionFlags.Rights
                | WiredPermissionFlags.GroupMembers
                | WiredPermissionFlags.GroupAdmins;

            if (
                (modifyPermissionMask & ~(allFlags & ~WiredPermissionFlags.Everyone)) != 0
                || (readPermissionMask & ~allFlags) != 0
                || timezone is null
                || timezone.Length > RoomEntity.WIRED_TIMEZONE_MAX_LENGTH
            )
            {
                _logger.LogWarning(
                    "Rejected wired settings for room {RoomId} by {PlayerId}: modify {Modify}, read {Read}, timezone length {TimezoneLength}",
                    _state.RoomId,
                    ctx.PlayerId,
                    modifyPermissionMask,
                    readPermissionMask,
                    timezone?.Length ?? -1
                );

                return false;
            }

            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            await dbCtx
                .Rooms.Where(x => x.Id == _state.RoomId.Value)
                .ExecuteUpdateAsync(
                    up =>
                        up.SetProperty(r => r.WiredModifyPermissionMask, modifyPermissionMask)
                            .SetProperty(r => r.WiredReadPermissionMask, readPermissionMask)
                            .SetProperty(r => r.WiredTimezone, timezone),
                    ct
                );

            _state.RoomSnapshot = _state.RoomSnapshot with
            {
                WiredModifyPermissionMask = modifyPermissionMask,
                WiredReadPermissionMask = readPermissionMask,
                WiredTimezone = timezone,
            };

            await _grainFactory.SendComposerToPlayerAsync(
                ctx.PlayerId,
                CreateWiredRoomSettingsComposer(),
                ct
            );

            await SecurityModule.RefreshWiredPermissionsForRoomAsync(ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to set wired settings for room {RoomId} by {PlayerId}",
                _state.RoomId,
                ctx.PlayerId
            );

            return false;
        }
    }

    public async Task<WiredRoomStatsSnapshot?> GetWiredRoomStatsAsync(
        ActionContext ctx,
        CancellationToken ct
    )
    {
        if (!await CanReadWiredAsync(ctx, ct))
            return null;

        var (floorItemCount, wallItemCount) = WiredSystem.CountWiredItems();
        var variables = (await WiredSystem.GetWiredVariablesSnapshotAsync(ct)).Variables;
        var executionCost = WiredSystem.GetExecutionCost(NowMs());

        return new WiredRoomStatsSnapshot
        {
            ExecutionCost = executionCost,
            ExecutionCostCap = _roomConfig.WiredExecutionCostCap,
            IsHeavy = executionCost >= _roomConfig.WiredExecutionCostCap,
            FloorItemCount = floorItemCount,
            FloorItemCap = _roomConfig.WiredMaxFloorItems,
            WallItemCount = wallItemCount,
            WallItemCap = _roomConfig.WiredMaxWallItems,
            PermanentFurniVariables = RoomWiredSystem.CountPermanentVariables(
                variables,
                WiredVariableTargetType.Furni
            ),
            MaxPermanentFurniVariables = _roomConfig.WiredMaxPermanentFurniVariables,
            PermanentUserVariables = RoomWiredSystem.CountPermanentVariables(
                variables,
                WiredVariableTargetType.User
            ),
            MaxPermanentUserVariables = _roomConfig.WiredMaxPermanentUserVariables,
            PermanentGlobalVariables = RoomWiredSystem.CountPermanentVariables(
                variables,
                WiredVariableTargetType.Global
            ),
            MaxPermanentGlobalVariables = _roomConfig.WiredMaxPermanentGlobalVariables,
        };
    }

    public async Task<ImmutableArray<WiredErrorLogSnapshot>?> GetWiredErrorLogsAsync(
        ActionContext ctx,
        CancellationToken ct
    )
    {
        if (!await CanReadWiredAsync(ctx, ct))
            return null;

        return WiredSystem.GetErrorLogs(NowMs());
    }

    public async Task<bool> ClearWiredErrorLogsAsync(ActionContext ctx, CancellationToken ct)
    {
        if (!await CanModifyWiredAsync(ctx, ct))
            return false;

        WiredSystem.ClearErrorLogs();

        return true;
    }

    public async Task<WiredVariableInfoAndHoldersSnapshot?> GetWiredVariableHoldersAsync(
        ActionContext ctx,
        WiredVariableId variableId,
        CancellationToken ct
    )
    {
        if (!await CanReadWiredAsync(ctx, ct))
            return null;

        return WiredSystem.GetVariableHolders(variableId);
    }

    public async Task<bool> SetWiredVariableValueAsync(
        ActionContext ctx,
        WiredVariableBinding binding,
        WiredVariableId variableId,
        WiredVariableValue value,
        CancellationToken ct
    )
    {
        try
        {
            if (!await CanModifyWiredAsync(ctx, ct))
                return false;

            return await WiredSystem.SetVariableValueAsync(binding, variableId, value, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Player {PlayerId} failed to set wired variable {VariableId} on {TargetType} {TargetId} in room {RoomId}",
                ctx.PlayerId,
                variableId,
                binding.TargetType,
                binding.TargetId,
                _state.RoomId
            );

            return false;
        }
    }

    private async Task<bool> CanReadWiredAsync(ActionContext ctx, CancellationToken ct)
    {
        var (_, canRead) = await GetWiredPermissionsAsync(ctx, ct);

        return canRead;
    }

    private async Task<bool> CanModifyWiredAsync(ActionContext ctx, CancellationToken ct)
    {
        var (canModify, _) = await GetWiredPermissionsAsync(ctx, ct);

        return canModify;
    }

    private async Task<(bool canModify, bool canRead)> GetWiredPermissionsAsync(
        ActionContext ctx,
        CancellationToken ct
    )
    {
        await SecurityModule.EnsureRightsLoadedAsync(ct);

        return SecurityModule.GetWiredPermissions(
            await SecurityModule.GetControllerLevelAsync(ctx)
        );
    }

    private WiredRoomSettingsSnapshot CreateWiredRoomSettingsSnapshot() =>
        new()
        {
            ModifyPermissionMask = _state.RoomSnapshot.WiredModifyPermissionMask,
            ReadPermissionMask = _state.RoomSnapshot.WiredReadPermissionMask,
            Timezone = _state.RoomSnapshot.WiredTimezone,
        };

    private WiredRoomSettingsEventMessageComposer CreateWiredRoomSettingsComposer() =>
        new()
        {
            ModifyPermissionMask = _state.RoomSnapshot.WiredModifyPermissionMask,
            ReadPermissionMask = _state.RoomSnapshot.WiredReadPermissionMask,
            Timezone = _state.RoomSnapshot.WiredTimezone,
        };
}
