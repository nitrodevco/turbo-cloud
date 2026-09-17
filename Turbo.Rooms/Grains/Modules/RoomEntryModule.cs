using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Rooms.Grains.Modules;

/// <summary>
/// Decides whether a player may enter the room (bans, capacity, door mode) and owns the doorbell
/// queue: players waiting outside a locked door until someone with rights answers.
/// </summary>
public sealed class RoomEntryModule(
    RoomGrain roomGrain,
    IDbContextFactory<TurboDbContext> dbCtxFactory
)
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;

    public async Task<RoomEntryAccessType> CheckAccessAsync(
        PlayerId playerId,
        string? password,
        bool bypassDoor
    )
    {
        var controllerLevel = await _roomGrain.SecurityModule.GetControllerLevelAsync(playerId);
        var snapshot = _roomGrain._state.RoomSnapshot;

        // A player with an avatar here is reloading the room they are already standing in. They
        // already hold a slot and are already past the door, so counting them against capacity or
        // sending them back to the doorbell would eject them from a room they legitimately
        // occupy. Bans still apply, so a ban placed while they are inside takes effect.
        var isReentering = _roomGrain._state.AvatarsByPlayerId.ContainsKey(playerId);

        if (controllerLevel < RoomControllerType.Owner)
        {
            if (GetIsBanned(playerId))
                return RoomEntryAccessType.Banned;

            if (!isReentering && snapshot.PlayersMax > 0)
            {
                var population = await _roomGrain.GetRoomPopulationAsync(CancellationToken.None);

                if (population >= snapshot.PlayersMax)
                    return RoomEntryAccessType.Full;
            }
        }

        if (isReentering || bypassDoor || controllerLevel >= RoomControllerType.Rights)
            return RoomEntryAccessType.Allowed;

        return snapshot.DoorMode switch
        {
            RoomDoorModeType.Locked => RoomEntryAccessType.Doorbell,
            RoomDoorModeType.Password => CheckPassword(snapshot.Password, password),
            _ => RoomEntryAccessType.Allowed,
        };
    }

    /// <summary>
    /// Registers the player at the door and notifies every controller currently in the room.
    /// Returns false when nobody is present to answer, in which case the player is not queued.
    /// </summary>
    public async Task<bool> RingDoorbellAsync(
        PlayerId playerId,
        string playerName,
        CancellationToken ct
    )
    {
        RemoveDoorbellRinger(playerId);

        var controllerIds = await GetPresentControllerIdsAsync();

        if (controllerIds.Count == 0)
            return false;

        _roomGrain._state.DoorbellRingersByName[playerName] = playerId;

        await _roomGrain.SendComposerToPlayersAsync(
            controllerIds,
            new DoorbellMessageComposer { Username = playerName },
            ct
        );

        return true;
    }

    /// <summary>
    /// Resolves a doorbell answer from a controller. Returns the waiting player's id, or null when
    /// the answerer lacks rights or nobody by that name is waiting. Other controllers are told the
    /// outcome so their doorbell prompt closes.
    /// </summary>
    public async Task<PlayerId?> AnswerDoorbellAsync(
        ActionContext ctx,
        string playerName,
        bool accepted,
        CancellationToken ct
    )
    {
        var controllerLevel = await _roomGrain.SecurityModule.GetControllerLevelAsync(ctx);

        if (controllerLevel < RoomControllerType.Rights)
            return null;

        if (!_roomGrain._state.DoorbellRingersByName.Remove(playerName, out var ringerId))
            return null;

        var controllerIds = await GetPresentControllerIdsAsync();

        IComposer composer = accepted
            ? new FlatAccessibleMessageComposer
            {
                RoomId = _roomGrain.RoomId,
                Username = playerName,
            }
            : new FlatAccessDeniedMessageComposer
            {
                RoomId = _roomGrain.RoomId,
                Username = playerName,
            };

        await _roomGrain.SendComposerToPlayersAsync(controllerIds, composer, ct);

        return ringerId;
    }

    public void RemoveDoorbellRinger(PlayerId playerId)
    {
        var ringers = _roomGrain._state.DoorbellRingersByName;

        foreach (var name in ringers.Where(x => x.Value == playerId).Select(x => x.Key).ToList())
            ringers.Remove(name);
    }

    public bool GetIsBanned(PlayerId playerId)
    {
        if (!_roomGrain._state.BannedUntilByPlayerId.TryGetValue(playerId, out var bannedUntil))
            return false;

        if (bannedUntil > DateTime.UtcNow)
            return true;

        _roomGrain._state.BannedUntilByPlayerId.Remove(playerId);

        return false;
    }

    internal async Task EnsureBansLoadedAsync(CancellationToken ct)
    {
        if (_roomGrain._state.IsBansLoaded)
            return;

        var now = DateTime.UtcNow;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entities = await dbCtx
            .RoomBans.AsNoTracking()
            .Where(x => x.RoomEntityId == _roomGrain.RoomId.Value && x.DateExpires > now)
            .ToListAsync(ct);

        foreach (var entity in entities)
            _roomGrain._state.BannedUntilByPlayerId[PlayerId.Parse(entity.PlayerEntityId)] =
                entity.DateExpires;

        _roomGrain._state.IsBansLoaded = true;
    }

    private static RoomEntryAccessType CheckPassword(string expected, string? provided)
    {
        if (string.IsNullOrEmpty(expected))
            return RoomEntryAccessType.Allowed;

        if (string.IsNullOrEmpty(provided))
            return RoomEntryAccessType.PasswordRequired;

        return string.Equals(expected, provided, StringComparison.Ordinal)
            ? RoomEntryAccessType.Allowed
            : RoomEntryAccessType.InvalidPassword;
    }

    private async Task<List<PlayerId>> GetPresentControllerIdsAsync()
    {
        var result = new List<PlayerId>();

        foreach (var playerId in _roomGrain._state.AvatarsByPlayerId.Keys)
        {
            var controllerLevel = await _roomGrain.SecurityModule.GetControllerLevelAsync(playerId);

            if (controllerLevel >= RoomControllerType.Rights)
                result.Add(playerId);
        }

        return result;
    }
}
