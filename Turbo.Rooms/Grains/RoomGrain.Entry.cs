using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Messages.Outgoing.Room.Action;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Permissions;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents.Wiredmenu;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Snapshots;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Grains;

public sealed partial class RoomGrain
{
    public async Task<RoomEntryAccessType> CheckEntryAccessAsync(
        PlayerId playerId,
        string? password,
        bool bypassDoor,
        CancellationToken ct
    )
    {
        try
        {
            return await EntryModule.CheckAccessAsync(playerId, password, bypassDoor);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to check entry access for player {PlayerId} in room {RoomId}",
                playerId,
                _state.RoomId
            );

            throw;
        }
    }

    public async Task<bool> RingDoorbellAsync(
        PlayerId playerId,
        string playerName,
        CancellationToken ct
    )
    {
        try
        {
            return await EntryModule.RingDoorbellAsync(playerId, playerName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to ring doorbell for player {PlayerId} in room {RoomId}",
                playerId,
                _state.RoomId
            );

            return false;
        }
    }

    public async Task<PlayerId?> AnswerDoorbellAsync(
        ActionContext ctx,
        string playerName,
        bool accepted,
        CancellationToken ct
    )
    {
        try
        {
            return await EntryModule.AnswerDoorbellAsync(ctx, playerName, accepted, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to answer doorbell for {PlayerName} (accepted:{Accepted}) by {PlayerId} in room {RoomId}",
                playerName,
                accepted,
                ctx.PlayerId,
                _state.RoomId
            );

            return null;
        }
    }

    public Task<RoomEntryViewSnapshot> GetEntryViewAsync(PlayerId playerId, CancellationToken ct) =>
        Task.FromResult(
            new RoomEntryViewSnapshot
            {
                Room = _state.RoomSnapshot,
                Map = MapModule.GetMapSnapshot(ct),
                OwnerNames = FurniModule.GetOwnerNames(),
                FloorItems = FurniModule.GetShownFloorItemSnapshots(),
                WallItems = FurniModule.GetShownWallItemSnapshots(),
                Avatars = AvatarModule.GetAvatarSnapshots(),
                Properties = [.. _state.RoomProperties],
                AreaHides =
                [
                    .. FurniModule
                        .Items.Select(x => (x.Logic as FurnitureAreaHideLogic)?.GetActiveArea())
                        .OfType<AreaHideDataSnapshot>(),
                ],
            }
        );

    /// <summary>
    /// The end of a player's entry, after the room itself (RoomReady through Items), for them
    /// alone and in Habbo's order (official client captures, 32 of 32 entries on builds 1709,
    /// 1318 and 1745): their own avatar, RoomEntryInfo, RoomEvent, ConfigurationItemStates,
    /// WiredEnvironment, FurnitureLabelsAndRegions, YouAreController, WiredPermissions,
    /// YouAreOwner, RoomRating, RaidProtectionCapability. A visitor without rights is told
    /// neither YouAreController nor YouAreNotController (6 of 6 such entries). No UserUpdate
    /// comes before the raid protection (0 of 32), so what the others are doing follows it.
    /// Built in the turn that made the avatar, so its level is the avatar's own.
    /// </summary>
    private async Task<ImmutableArray<IComposer>> BuildEntryTailAsync(
        IRoomPlayer avatar,
        CancellationToken ct
    )
    {
        var playerId = avatar.PlayerId;

        await SecurityModule.EnsureRightsLoadedAsync(ct);

        var controllerLevel = await SecurityModule.TakeEntryControllerLevelAsync(playerId);
        var (canModifyWired, canReadWired) = WiredSystem.GetPermissions(controllerLevel);
        var isOwner = controllerLevel >= RoomControllerType.Owner;
        var others = AvatarModule
            .GetAvatarSnapshots()
            .Where(x => x.ObjectId != avatar.ObjectId)
            .ToList();
        var tail = new List<IComposer>
        {
            new UsersMessageComposer { Avatars = [avatar.GetSnapshot()] },
            // The client answers this with GetGuestRoom(enterRoom: true), which is what fills its
            // room info window and the owner-only settings buttons.
            new RoomEntryInfoMessageComposer { RoomId = _state.RoomId, IsOwner = isOwner },
            // Sent with no event too, as Habbo does, so the last room's event card goes.
            new RoomEventMessageComposer { Event = GetActiveEvent(), SentAtUtc = DateTime.UtcNow },
        };

        // Where Habbo sends it is unrecorded (no muted room was entered); with the room's other
        // switches is inferred.
        if (_state.IsRoomMuted)
            tail.Add(new MuteAllInRoomEventMessageComposer { IsMuted = true });

        // Sent on every entry, off or on, as Habbo does.
        tail.Add(FurnitureConfigurationControlLogic.GetConfigurationItemStates(FurniModule));
        tail.AddRange(WiredSystem.GetEntryState());

        if (controllerLevel >= RoomControllerType.Rights)
            tail.Add(
                new YouAreControllerMessageComposer
                {
                    RoomId = _state.RoomId,
                    ControllerLevel = controllerLevel,
                }
            );

        tail.Add(
            new WiredPermissionsEventMessageComposer
            {
                CanModify = canModifyWired,
                CanRead = canReadWired,
            }
        );

        if (isOwner)
            tail.Add(new YouAreOwnerMessageComposer { RoomId = _state.RoomId });

        tail.Add(
            new RoomRatingMessageComposer
            {
                Rating = _state.RoomSnapshot.Score,
                CanRate = CanRate(playerId),
            }
        );

        // After the entry info: the client takes this only for the room it has entered, which
        // that message is what tells it, and shows Room info's raid protection button on it.
        tail.Add(
            new RaidProtectionCapabilityMessageComposer
            {
                RoomId = _state.RoomId,
                CanManage = _roomConfig.RaidProtectionEnabled && isOwner,
            }
        );

        // Habbo sends Users even for an empty room, but no UserUpdate with nothing in it (hhus, 2026-10-13).
        if (others.Count > 0)
            tail.Add(new UserUpdateMessageComposer { Avatars = [.. others] });

        // Neither a dance, an effect nor a sleep travels in the Users packet, so all three are
        // replayed to the arriving player as their own updates. Any avatar can carry them; one
        // that the client does not draw as a user simply never has one to replay.
        tail.AddRange(
            others
                .Where(x => x.DanceType != AvatarDanceType.None)
                .Select(x => new DanceMessageComposer
                {
                    ObjectId = x.ObjectId,
                    DanceType = x.DanceType,
                })
        );
        tail.AddRange(
            others
                .Where(x => x.EffectId > 0)
                .Select(x => new AvatarEffectMessageComposer
                {
                    ObjectId = x.ObjectId,
                    EffectId = x.EffectId,
                    DelayMilliseconds = 0,
                })
        );
        tail.AddRange(
            others
                .Where(x => x.IsIdle)
                .Select(x => new SleepMessageComposer { ObjectId = x.ObjectId, IsSleeping = true })
        );

        return [.. tail];
    }

    public Task RemoveDoorbellRingerAsync(PlayerId playerId, CancellationToken ct)
    {
        EntryModule.RemoveDoorbellRinger(playerId);

        return Task.CompletedTask;
    }
}
