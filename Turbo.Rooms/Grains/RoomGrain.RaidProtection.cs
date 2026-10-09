using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Entities.Room;
using Turbo.Database.Extensions;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Rooms.Grains;

/// <summary>
/// Raid protection's settings: the owner reads and saves them through its window. Detecting a
/// raid is not built (nothing in the client or its texts says what counts as one), so no raid is
/// ever active and the last raid is whatever the row holds.
/// </summary>
public sealed partial class RoomGrain
{
    public async Task<bool> CanManageRaidProtectionAsync(ActionContext ctx, CancellationToken ct)
    {
        if (!_roomConfig.RaidProtectionEnabled)
            return false;

        await SecurityModule.EnsureRightsLoadedAsync(ct);

        return await SecurityModule.GetControllerLevelAsync(ctx) >= RoomControllerType.Owner;
    }

    public async Task<RaidProtectionSettingsSnapshot?> GetRaidProtectionSettingsAsync(
        ActionContext ctx,
        CancellationToken ct
    )
    {
        if (!await CanManageRaidProtectionAsync(ctx, ct))
            return null;

        return (await LoadRaidProtectionAsync(ct)).ToSnapshot(_state.RoomId, incidentActive: false);
    }

    public async Task<RaidProtectionSaveResultSnapshot> SaveRaidProtectionSettingsAsync(
        ActionContext ctx,
        RaidProtectionSettingsUpdateSnapshot update,
        CancellationToken ct
    )
    {
        if (!_roomConfig.RaidProtectionEnabled)
            return RaidResult(RaidProtectionSaveResultType.FeatureDisabled, NewRaidProtection());

        if (!await CanManageRaidProtectionAsync(ctx, ct))
            return RaidResult(RaidProtectionSaveResultType.NotAllowed, NewRaidProtection());

        var current = await LoadRaidProtectionAsync(ct);

        if (
            !Enum.IsDefined((RaidSensitivityType)update.DetectionSensitivity)
            || !Enum.IsDefined((RaidActionType)update.ActionType)
            || !Enum.IsDefined((RaidSensitivityType)update.GuardSensitivity)
            || Array.IndexOf(_roomConfig.RaidBanDurationsSeconds, update.BanDurationSeconds) < 0
            || Array.IndexOf(_roomConfig.RaidGuardDurationsSeconds, update.GuardDurationSeconds) < 0
        )
            return RaidResult(RaidProtectionSaveResultType.Invalid, current);

        if (update.Enabled && !current.Enabled && !update.Confirmed)
            return RaidResult(RaidProtectionSaveResultType.NotConfirmed, current);

        var next = new RoomRaidProtectionEntity
        {
            RoomEntityId = _state.RoomId.Value,
            Enabled = update.Enabled,
            DetectionSensitivity = (RaidSensitivityType)update.DetectionSensitivity,
            ActionType = (RaidActionType)update.ActionType,
            BanDurationSeconds = update.BanDurationSeconds,
            GuardEnabled = update.GuardEnabled,
            GuardDurationSeconds = update.GuardDurationSeconds,
            GuardSensitivity = (RaidSensitivityType)update.GuardSensitivity,
            LastRaidAt = current.LastRaidAt,
        };

        if (!await PersistRaidProtectionAsync(next, ct))
            return RaidResult(RaidProtectionSaveResultType.Failed, current);

        return RaidResult(RaidProtectionSaveResultType.Saved, next);
    }

    private RaidProtectionSaveResultSnapshot RaidResult(
        RaidProtectionSaveResultType result,
        RoomRaidProtectionEntity settings
    ) =>
        new()
        {
            Result = result,
            Settings = settings.ToSnapshot(_state.RoomId, incidentActive: false),
        };

    /// <summary>The room's row, or a new one with the defaults when it has none.</summary>
    private async Task<RoomRaidProtectionEntity> LoadRaidProtectionAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        return await dbCtx
                .RoomRaidProtections.AsNoTracking()
                .FirstOrDefaultAsync(x => x.RoomEntityId == _state.RoomId.Value, ct)
            ?? NewRaidProtection();
    }

    /// <summary>A room without a row has the defaults a new row starts from.</summary>
    private RoomRaidProtectionEntity NewRaidProtection() =>
        new() { RoomEntityId = _state.RoomId.Value };

    private async Task<bool> PersistRaidProtectionAsync(
        RoomRaidProtectionEntity next,
        CancellationToken ct
    )
    {
        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            var entity = await dbCtx.RoomRaidProtections.FirstOrDefaultAsync(
                x => x.RoomEntityId == next.RoomEntityId,
                ct
            );

            if (entity is null)
            {
                dbCtx.RoomRaidProtections.Add(next);
            }
            else
            {
                entity.Enabled = next.Enabled;
                entity.DetectionSensitivity = next.DetectionSensitivity;
                entity.ActionType = next.ActionType;
                entity.BanDurationSeconds = next.BanDurationSeconds;
                entity.GuardEnabled = next.GuardEnabled;
                entity.GuardDurationSeconds = next.GuardDurationSeconds;
                entity.GuardSensitivity = next.GuardSensitivity;
            }

            await dbCtx.SaveChangesAsync(ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save raid protection for room {RoomId}", _state.RoomId);

            return false;
        }
    }
}
