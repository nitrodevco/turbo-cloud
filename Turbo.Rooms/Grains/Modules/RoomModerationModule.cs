using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots;

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

        await StoreMuteAsync(playerId, durationMinutes, ct);

        return true;
    }

    /// <summary>A mute by the room itself (wired): no actor, no rank check, the room cap still applies.</summary>
    public async Task<bool> MutePlayerBySystemAsync(
        PlayerId playerId,
        int durationMinutes,
        CancellationToken ct
    )
    {
        if (durationMinutes <= 0 || !_roomGrain._state.AvatarsByPlayerId.ContainsKey(playerId))
            return false;

        var controllerLevel = await _roomGrain.SecurityModule.GetControllerLevelAsync(playerId);

        if (controllerLevel >= RoomControllerType.Owner)
            return false;

        await StoreMuteAsync(playerId, durationMinutes, ct);

        return true;
    }

    /// <summary>Writes the mute, capped at the room's maximum, and starts applying it.</summary>
    private async Task StoreMuteAsync(PlayerId playerId, int durationMinutes, CancellationToken ct)
    {
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

    public async Task<bool> ToggleRoomMuteAsync(ActionContext ctx, CancellationToken ct)
    {
        var controllerLevel = await _roomGrain.SecurityModule.GetControllerLevelAsync(ctx);

        if (controllerLevel < RoomControllerType.Owner)
            return false;

        _roomGrain._state.IsRoomMuted = !_roomGrain._state.IsRoomMuted;

        await _roomGrain.SendComposerToRoomAsync(
            new MuteAllInRoomEventMessageComposer { IsMuted = _roomGrain._state.IsRoomMuted },
            ct
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

    public async Task<bool> KickPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        if (
            !await CanModerateAsync(
                ctx,
                playerId,
                _roomGrain._state.RoomSnapshot.ModSettings.WhoCanKick
            )
        )
            return false;

        if (!_roomGrain._state.AvatarsByPlayerId.ContainsKey(playerId))
            return false;

        await _roomGrain.AvatarModule.RemoveAvatarFromPlayerAsync(ctx, playerId, ct);

        return true;
    }

    /// <summary>
    /// A kick by the room itself (wired): no actor and no "who can kick" setting, but the owner
    /// and anyone above them stay, as with <see cref="MutePlayerBySystemAsync"/>. It runs inside
    /// the room tick, so the player's session is told to close without being awaited: the
    /// presence may itself be waiting on this room. The parting words, when there are any, are
    /// whispered to the player first.
    /// </summary>
    public async Task<bool> KickPlayerBySystemAsync(
        PlayerId playerId,
        string partingWords,
        CancellationToken ct
    )
    {
        if (!_roomGrain.AvatarModule.TryGetPlayer(playerId, out var player))
            return false;

        var controllerLevel = await _roomGrain.SecurityModule.GetControllerLevelAsync(playerId);

        if (controllerLevel >= RoomControllerType.Owner)
            return false;

        if (partingWords.Length > 0)
            await _roomGrain.ChatSystem.WhisperToPlayerAsync(player, partingWords, ct);

        await _roomGrain.AvatarModule.RemoveAvatarFromPlayerAsync(
            ActionContext.CreateForSystem(_roomGrain.RoomId),
            playerId,
            ct
        );

        _roomGrain
            ._grainFactory.GetPlayerPresenceGrain(playerId)
            .OnRemovedFromRoomAsync(_roomGrain.RoomId, true, CancellationToken.None)
            .LogAndForget(
                _roomGrain._logger,
                $"close the room session of player {playerId} kicked from room {_roomGrain.RoomId}"
            );

        return true;
    }

    public async Task<bool> BanPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        RoomBanDurationType duration,
        CancellationToken ct
    )
    {
        if (
            !await CanModerateAsync(
                ctx,
                playerId,
                _roomGrain._state.RoomSnapshot.ModSettings.WhoCanBan
            )
        )
            return false;

        var config = _roomGrain._roomConfig;
        var expiresAt = duration switch
        {
            RoomBanDurationType.Hour => DateTime.UtcNow.AddMinutes(config.BanHourMinutes),
            RoomBanDurationType.Day => DateTime.UtcNow.AddMinutes(config.BanDayMinutes),
            RoomBanDurationType.Permanent => DateTime.UtcNow.AddDays(config.BanPermanentDays),
            _ => throw new ArgumentOutOfRangeException(nameof(duration), duration, null),
        };

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entity = await dbCtx.RoomBans.FirstOrDefaultAsync(
            x => x.RoomEntityId == _roomGrain.RoomId.Value && x.PlayerEntityId == playerId.Value,
            ct
        );

        if (entity is null)
        {
            dbCtx.RoomBans.Add(
                new RoomBanEntity
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

        _roomGrain._state.BannedUntilByPlayerId[playerId] = expiresAt;

        if (_roomGrain._state.AvatarsByPlayerId.ContainsKey(playerId))
            await _roomGrain.AvatarModule.RemoveAvatarFromPlayerAsync(ctx, playerId, ct);

        return true;
    }

    public async Task<bool> UnbanPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        if (playerId <= 0 || !await CanManageBansAsync(ctx))
            return false;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        await dbCtx
            .RoomBans.Where(x =>
                x.RoomEntityId == _roomGrain.RoomId.Value && x.PlayerEntityId == playerId.Value
            )
            .ExecuteDeleteAsync(ct);

        return _roomGrain._state.BannedUntilByPlayerId.Remove(playerId);
    }

    public async Task<ImmutableArray<RoomBannedPlayerSnapshot>?> GetBannedPlayersAsync(
        ActionContext ctx,
        CancellationToken ct
    )
    {
        if (!await CanManageBansAsync(ctx))
            return null;

        var now = DateTime.UtcNow;
        var bannedIds = _roomGrain
            ._state.BannedUntilByPlayerId.Where(x => x.Value > now)
            .Select(x => x.Key)
            .ToList();

        if (bannedIds.Count == 0)
            return [];

        var names = await _roomGrain
            ._grainFactory.GetPlayerDirectoryGrain()
            .GetPlayerNamesAsync(bannedIds, ct);

        return
        [
            .. bannedIds.Select(id => new RoomBannedPlayerSnapshot
            {
                PlayerId = id,
                Name = names.TryGetValue(id, out var name) ? name : string.Empty,
            }),
        ];
    }

    /// <summary>The ban list is part of room settings: owners, or whoever the ban setting allows.</summary>
    private async Task<bool> CanManageBansAsync(ActionContext ctx)
    {
        var level = await _roomGrain.SecurityModule.GetControllerLevelAsync(ctx);

        if (level >= RoomControllerType.Owner)
            return true;

        return _roomGrain._state.RoomSnapshot.ModSettings.WhoCanBan switch
        {
            ModSettingType.All => true,
            ModSettingType.Rights => level >= RoomControllerType.Rights,
            ModSettingType.GroupRights => level >= RoomControllerType.GroupRights,
            ModSettingType.RightsOrGroup => level >= RoomControllerType.Rights,
            _ => false,
        };
    }

    public async Task<ImmutableArray<string>?> GetRoomFilterWordsAsync(
        ActionContext ctx,
        CancellationToken ct
    )
    {
        if (!await _roomGrain.SecurityModule.GetIsRoomOwnerAsync(ctx))
            return null;

        await EnsureFilterLoadedAsync(ct);

        return [.. _roomGrain._state.FilterWords.Order(StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<bool> UpdateRoomFilterAsync(
        ActionContext ctx,
        bool isAdding,
        string word,
        CancellationToken ct
    )
    {
        if (!await _roomGrain.SecurityModule.GetIsRoomOwnerAsync(ctx))
            return false;

        await EnsureFilterLoadedAsync(ct);

        var config = _roomGrain._roomConfig;
        var words = _roomGrain._state.FilterWords;

        word = word.Trim();

        if (
            word.Length == 0
            || word.Length
                > Math.Min(config.RoomFilterWordMaxLength, RoomFilterWordEntity.WORD_MAX_LENGTH)
            || (isAdding && !words.Contains(word) && words.Count >= config.RoomFilterMaxWords)
        )
        {
            _roomGrain._logger.LogWarning(
                "Rejected room filter change in room {RoomId} by player {PlayerId}: adding={IsAdding}, {Length} characters, {Count} words",
                _roomGrain.RoomId,
                ctx.PlayerId,
                isAdding,
                word.Length,
                words.Count
            );

            return false;
        }

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        if (isAdding)
        {
            if (!words.Add(word))
                return true;

            dbCtx.RoomFilterWords.Add(
                new RoomFilterWordEntity
                {
                    RoomEntityId = _roomGrain.RoomId.Value,
                    Word = word,
                    RoomEntity = null!,
                }
            );

            await dbCtx.SaveChangesAsync(ct);
        }
        else
        {
            if (!words.Remove(word))
                return true;

            await dbCtx
                .RoomFilterWords.Where(x =>
                    x.RoomEntityId == _roomGrain.RoomId.Value && x.Word == word
                )
                .ExecuteDeleteAsync(ct);
        }

        return true;
    }

    /// <summary>Replaces each filtered word in the text; whole words only, case-insensitive.</summary>
    public string ApplyFilter(string text)
    {
        var words = _roomGrain._state.FilterWords;

        if (words.Count == 0 || text.Length == 0)
            return text;

        var replacement = _roomGrain._roomConfig.RoomFilterReplacement;
        var parts = text.Split(' ');
        var changed = false;

        for (var i = 0; i < parts.Length; i++)
        {
            if (!words.Contains(parts[i]))
                continue;

            parts[i] = replacement;
            changed = true;
        }

        return changed ? string.Join(' ', parts) : text;
    }

    internal async Task EnsureFilterLoadedAsync(CancellationToken ct)
    {
        if (_roomGrain._state.IsFilterLoaded)
            return;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var words = await dbCtx
            .RoomFilterWords.AsNoTracking()
            .Where(x => x.RoomEntityId == _roomGrain.RoomId.Value)
            .Select(x => x.Word)
            .ToListAsync(ct);

        _roomGrain._state.FilterWords.UnionWith(words);
        _roomGrain._state.IsFilterLoaded = true;
    }

    private Task<bool> CanMutePlayerAsync(ActionContext ctx, PlayerId targetId) =>
        CanModerateAsync(ctx, targetId, _roomGrain._state.RoomSnapshot.ModSettings.WhoCanMute);

    /// <summary>
    /// Whether the actor may apply a moderation action to the target under the room's setting
    /// for it. Owners always may; nobody may act on someone of equal or higher rank.
    /// </summary>
    private async Task<bool> CanModerateAsync(
        ActionContext ctx,
        PlayerId targetId,
        ModSettingType setting
    )
    {
        if (targetId <= 0 || ctx.PlayerId == targetId)
            return false;

        var actorLevel = await _roomGrain.SecurityModule.GetControllerLevelAsync(ctx);
        var targetLevel = await _roomGrain.SecurityModule.GetControllerLevelAsync(targetId);

        if (targetLevel >= actorLevel)
            return false;

        if (actorLevel >= RoomControllerType.Owner)
            return true;

        return setting switch
        {
            ModSettingType.All => true,
            ModSettingType.Rights => actorLevel >= RoomControllerType.Rights,
            ModSettingType.GroupRights => actorLevel >= RoomControllerType.GroupRights,
            ModSettingType.RightsOrGroup => actorLevel >= RoomControllerType.Rights,
            _ => false,
        };
    }
}
