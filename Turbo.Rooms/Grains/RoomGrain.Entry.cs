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
using Turbo.Primitives.Rooms.Snapshots.Avatars;
using Turbo.Primitives.Rooms.Snapshots.Furniture;
using Turbo.Primitives.Rooms.Watching;
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
        Task.FromResult(TakeEntryView(ct));

    private RoomEntryViewSnapshot TakeEntryView(CancellationToken ct) =>
        new()
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
            WiredVisualization = [.. WiredSystem.GetVisualizationEntryState()],
        };

    /// <summary>
    /// The end of a player's entry, after the room itself (RoomReady through Items), for them
    /// alone and in Habbo's order (official client captures, 32 of 32 entries on builds 1709,
    /// 1318 and 1745): their own avatar, RoomEntryInfo, the wired text notice when the room has
    /// one (<see cref="Systems.RoomWiredSystem.GetTriggerTextNoticeAsync"/>), RoomEvent,
    /// ConfigurationItemStates, WiredEnvironment, FurnitureLabelsAndRegions, YouAreController, WiredPermissions,
    /// YouAreOwner, RoomRating, RaidProtectionCapability. A visitor without rights is told
    /// neither YouAreController nor YouAreNotController (6 of 6 such entries). No UserUpdate
    /// comes before the raid protection (0 of 32), so the statuses, their own and the others',
    /// follow it.
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
        };

        if (await WiredSystem.GetTriggerTextNoticeAsync(ct) is { } notice)
            tail.Add(notice);

        // Sent with no event too, as Habbo does, so the last room's event card goes.
        tail.Add(
            new RoomEventMessageComposer
            {
                Event = GetActiveEvent(),
                SentAtUtc = UtcNow.UtcDateTime,
            }
        );

        // A muted room sends no MuteAllInRoom on entry: the client learns it from the
        // GetGuestRoomResult it asks for after RoomEntryInfo (allInRoomMuted).

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
                CanManage = CanManageRaidProtection(controllerLevel),
            }
        );

        // The player's own status first, then everyone else's, in one UserUpdate after the raid
        // protection (official client captures, 53 of 54 entries: the own avatar's direction and
        // flatctrl, 0-466 ms after its Users; 2026-10-13 duo entries: own index first). Without
        // it the client keeps the Users direction, which it reads as an angle, and the avatar
        // stands facing the wall until it first moves.
        tail.Add(new UserUpdateMessageComposer { Avatars = [avatar.GetSnapshot(), .. others] });

        tail.AddRange(DancesOf(others));
        tail.AddRange(EffectsOf(others, playerId));
        tail.AddRange(SleepsOf(others));

        return [.. tail];
    }

    // Neither a dance, an effect nor a sleep travels in the Users packet, so all three are
    // replayed to an arriving player (or a watcher) as their own updates, in this order. Any
    // avatar can carry them; one that the client does not draw as a user simply never has one to
    // replay.
    private static IEnumerable<IComposer> DancesOf(IEnumerable<RoomAvatarSnapshot> avatars) =>
        avatars
            .Where(x => x.DanceType != AvatarDanceType.None)
            .Select(x => new DanceMessageComposer
            {
                ObjectId = x.ObjectId,
                DanceType = x.DanceType,
            });

    /// <summary>As <paramref name="viewer"/> is shown each (<c>IAvatarEffectPolicy.Show</c>).</summary>
    private IEnumerable<IComposer> EffectsOf(
        IEnumerable<RoomAvatarSnapshot> avatars,
        PlayerId viewer
    ) =>
        avatars
            .Where(x => x.EffectId > 0)
            .Select(x => new AvatarEffectMessageComposer
            {
                ObjectId = x.ObjectId,
                EffectId = AvatarModule.ShownTo(
                    viewer,
                    x.ObjectId,
                    x.AvatarType,
                    x.AvatarType == RoomObjectType.Player ? x.WebId : null,
                    x.EffectId
                ),
                DelayMilliseconds = 0,
            })
            .Where(x => x.EffectId > 0);

    private static IEnumerable<IComposer> SleepsOf(IEnumerable<RoomAvatarSnapshot> avatars) =>
        avatars
            .Where(x => x.IsIdle)
            .Select(x => new SleepMessageComposer { ObjectId = x.ObjectId, IsSleeping = true });

    /// <summary>
    /// A watcher's first view: the room as an entry shows it (<see cref="RoomEntryComposers.RoomOf"/>),
    /// then of the entry's tail what the room engine draws, in the tail's order: the configuration
    /// states (invisible furni), the WIRED labels, everyone's status, dances, effects and sleeps
    /// (AS3 <c>RoomMessageHandler.onConfigurationItemStates</c> and
    /// <c>onFurnitureLabelsAndRegions</c> hand them to the room engine). Left out is what only
    /// someone standing in the room is told: their own avatar, RoomEntryInfo (whose answer opens
    /// the room info), the wired notice, the event card, WiredEnvironment, rights, ownership,
    /// WiredPermissions, the rating and the raid protection. Taken in one turn, without awaiting,
    /// with the number of the last publish it already shows.
    /// </summary>
    public Task<RoomWatchView> GetWatchViewAsync(PlayerId watcherId, CancellationToken ct)
    {
        var shared = TakeSharedWatchView(ct);

        return Task.FromResult(
            new RoomWatchView
            {
                Sequence = shared.Sequence,
                Composers =
                [
                    .. shared.Before,
                    .. EffectsOf(shared.Avatars, watcherId),
                    .. shared.Sleeps,
                ],
            }
        );
    }

    /// <summary>
    /// The watch view's packets every watcher gets alike, taken again once the room has
    /// published since or a tick has passed (what a turn changes it publishes in that turn or
    /// on the next tick). Watchers starting together get the same instances, so the encoder's
    /// payload cache writes each once for all of them.
    /// </summary>
    private SharedWatchView TakeSharedWatchView(CancellationToken ct)
    {
        var nowMs = UtcNow.ToUnixTimeMilliseconds();

        if (
            _sharedWatchView is { } cached
            && cached.Sequence == _outboundSequence
            && nowMs - cached.TakenAtMs < _roomConfig.RoomTickMs
        )
            return cached;

        var view = TakeEntryView(ct);
        var avatars = view.Avatars;
        var before = new List<IComposer>(
            RoomEntryComposers.RoomOf(_state.RoomId, view, _roomConfig)
        )
        {
            FurnitureConfigurationControlLogic.GetConfigurationItemStates(FurniModule),
            WiredSystem.GetLabelsAndRegions(),
        };

        if (!avatars.IsDefaultOrEmpty)
            before.Add(new UserUpdateMessageComposer { Avatars = avatars });

        before.AddRange(DancesOf(avatars));

        return _sharedWatchView = new SharedWatchView(
            _outboundSequence,
            nowMs,
            avatars,
            [.. before],
            [.. SleepsOf(avatars)]
        );
    }

    private SharedWatchView? _sharedWatchView;

    private sealed record SharedWatchView(
        long Sequence,
        long TakenAtMs,
        ImmutableArray<RoomAvatarSnapshot> Avatars,
        ImmutableArray<IComposer> Before,
        ImmutableArray<IComposer> Sleeps
    );

    /// <summary>
    /// The arriving player's status for everyone already here, after the Users that drew them:
    /// Habbo's observers get it on the next room tick (official client capture, 2026-10-13 duo
    /// doorbell re-entry: Users then UserUpdate with direction 2 and flatctrl). The entry's own
    /// snapshot has taken the avatar's changes, so the tick would not send it.
    /// </summary>
    private Task SendEntryStatusToOthersAsync(IRoomPlayer avatar, CancellationToken ct) =>
        SendComposerToRoomExceptAsync(
            new UserUpdateMessageComposer { Avatars = [avatar.GetSnapshot()] },
            [avatar.PlayerId],
            ct
        );

    public Task RemoveDoorbellRingerAsync(PlayerId playerId, CancellationToken ct)
    {
        EntryModule.RemoveDoorbellRinger(playerId);

        return Task.CompletedTask;
    }
}
