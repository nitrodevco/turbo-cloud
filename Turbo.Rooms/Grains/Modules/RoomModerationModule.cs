using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Rooms.Grains.Modules;

/// <summary>
/// Owns room-level moderation state: per-player timed mutes (persisted in room_mutes) and the
/// session-only room-wide mute toggle.
/// </summary>
public sealed class RoomModerationModule(
    RoomGrain roomGrain,
    IDbContextFactory<TurboDbContext> dbCtxFactory
)
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;

    /// <summary>
    /// Seconds remaining on the player's personal mute, or 0 when the player is not muted.
    /// Expired entries are dropped on read.
    /// </summary>
    public int GetRemainingMuteSeconds(PlayerId playerId)
    {
        if (!_roomGrain._state.MutedUntilByPlayerId.TryGetValue(playerId, out var mutedUntil))
            return 0;

        var remaining = mutedUntil - DateTime.UtcNow;

        if (remaining <= TimeSpan.Zero)
        {
            _roomGrain._state.MutedUntilByPlayerId.Remove(playerId);

            return 0;
        }

        return (int)Math.Ceiling(remaining.TotalSeconds);
    }

    /// <summary>
    /// True when the room-wide mute is active and the player has no rights in the room.
    /// </summary>
    public async Task<bool> IsSilencedByRoomMuteAsync(PlayerId playerId)
    {
        if (!_roomGrain._state.IsRoomMuted)
            return false;

        var controllerLevel = await _roomGrain.SecurityModule.GetControllerLevelAsync(playerId);

        return controllerLevel < RoomControllerType.Rights;
    }

    public async Task<bool> MutePlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        int durationMinutes,
        CancellationToken ct
    )
    {
        if (durationMinutes <= 0 || !await CanMutePlayerAsync(ctx, playerId))
            return false;

        durationMinutes = Math.Min(
            durationMinutes,
            _roomGrain._roomConfig.ChatMuteMaxDurationMinutes
        );

        var expiresAt = DateTime.UtcNow.AddMinutes(durationMinutes);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entity = await dbCtx.RoomMutes.FirstOrDefaultAsync(
            x => x.RoomEntityId == _roomGrain.RoomId.Value && x.PlayerEntityId == playerId.Value,
            ct
        );

        if (entity is null)
        {
            dbCtx.RoomMutes.Add(
                new RoomMuteEntity
                {
                    RoomEntityId = _roomGrain.RoomId.Value,
                    PlayerEntityId = playerId.Value,
                    DateExpires = expiresAt,
                    RoomEntity = null!,
                    PlayerEntity = null!,
                }
            );
        }
        else
        {
            entity.DateExpires = expiresAt;
        }

        await dbCtx.SaveChangesAsync(ct);

        _roomGrain._state.MutedUntilByPlayerId[playerId] = expiresAt;

        return true;
    }

    public async Task<bool> UnmutePlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        if (!await CanMutePlayerAsync(ctx, playerId))
            return false;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        await dbCtx
            .RoomMutes.Where(x =>
                x.RoomEntityId == _roomGrain.RoomId.Value && x.PlayerEntityId == playerId.Value
            )
            .ExecuteDeleteAsync(ct);

        return _roomGrain._state.MutedUntilByPlayerId.Remove(playerId);
    }

    public async Task<bool> ToggleRoomMuteAsync(ActionContext ctx)
    {
        var controllerLevel = await _roomGrain.SecurityModule.GetControllerLevelAsync(ctx);

        if (controllerLevel < RoomControllerType.Owner)
            return false;

        _roomGrain._state.IsRoomMuted = !_roomGrain._state.IsRoomMuted;

        await _roomGrain.SendComposerToRoomAsync(
            new MuteAllInRoomEventMessageComposer { IsMuted = _roomGrain._state.IsRoomMuted }
        );

        return true;
    }

    internal async Task EnsureMutesLoadedAsync(CancellationToken ct)
    {
        if (_roomGrain._state.IsMutesLoaded)
            return;

        var now = DateTime.UtcNow;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entities = await dbCtx
            .RoomMutes.AsNoTracking()
            .Where(x => x.RoomEntityId == _roomGrain.RoomId.Value && x.DateExpires > now)
            .ToListAsync(ct);

        foreach (var entity in entities)
            _roomGrain._state.MutedUntilByPlayerId[PlayerId.Parse(entity.PlayerEntityId)] =
                entity.DateExpires;

        _roomGrain._state.IsMutesLoaded = true;
    }

    private async Task<bool> CanMutePlayerAsync(ActionContext ctx, PlayerId targetId)
    {
        if (targetId <= 0 || ctx.PlayerId == targetId)
            return false;

        var actorLevel = await _roomGrain.SecurityModule.GetControllerLevelAsync(ctx);
        var targetLevel = await _roomGrain.SecurityModule.GetControllerLevelAsync(targetId);

        if (targetLevel >= actorLevel)
            return false;

        if (actorLevel >= RoomControllerType.Owner)
            return true;

        return _roomGrain._state.RoomSnapshot.ModSettings.WhoCanMute switch
        {
            ModSettingType.All => true,
            ModSettingType.Rights => actorLevel >= RoomControllerType.Rights,
            ModSettingType.GroupRights => actorLevel >= RoomControllerType.GroupRights,
            ModSettingType.RightsOrGroup => actorLevel >= RoomControllerType.Rights,
            _ => false,
        };
    }
}
