using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Game;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Wired.VariableFx;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons.VariableFx;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Wired.VariableFx;

/// <summary>
/// Shows wired variables over avatars and furni. The variable fx addons in the room say how an
/// fx looks and who may see it; this system works out, per player in the room, which values
/// that player should be seeing, and sends only what changed since it last told them.
///
/// The client keeps nothing across rooms and filters nothing: a player is sent every config
/// when they enter, then statuses for exactly the holders their audience setting lets them see,
/// and a removal when a value, a holder or their right to see it goes away. All of it is
/// recomputed from the room as it is, on a short interval and only after something changed, so
/// there is no per-change bookkeeping to go wrong; what was sent is remembered per viewer and
/// the difference goes out as one batch.
///
/// Two things about the client shape the timing. It drops a status for an avatar or furni it
/// has not been told about yet, and never asks again; the object itself reaches it over the
/// room stream while these messages go to the player directly, so a new player, avatar or
/// furni waits <see cref="Configuration.WiredConfig.VariableFxEntryDelayMs"/> before anything
/// about it is sent. And a status marked "initialize" is drawn without the change animation,
/// which is what a player walking in, or a holder walking in, should get.
/// </summary>
public sealed class RoomVariableFxSystem(RoomGrain roomGrain)
    : RoomGrainComponent(roomGrain),
        IRoomEventListener,
        IRoomPlacementLimit
{
    private readonly Dictionary<int, VariableFxBinding> _bindingsByConfigId = [];
    private readonly Dictionary<PlayerId, VariableFxViewer> _viewersByPlayerId = [];

    // Players, and the avatars and furni, the client is still being told about: when each may
    // first be sent or shown.
    private readonly Dictionary<PlayerId, long> _playerReadyAtMs = [];
    private readonly Dictionary<int, long> _furniReadyAtMs = [];

    // The viewers of the flush under way, beside the player each one is.
    private readonly List<(IRoomPlayer Player, VariableFxViewer Viewer)> _flushViewers = [];

    // The fx past their variable box's limit when the bindings were last built.
    private readonly HashSet<RoomObjectId> _overLimitIds = [];

    // The fx placed since the last flush, whose first statuses are drawn as a sync.
    private readonly HashSet<int> _newConfigIds = [];

    // The values written since the last flush, changed or not: each is shown again, as Habbo does
    // (official client capture 2026-10-24, WIN63-202610201032, wired matrix addon.1200.R1: set 30
    // then 30 again sent a VariableFxStatus both times, 4/4).
    private readonly HashSet<(WiredVariableId VariableId, bool IsUser, int HolderId)> _written = [];

    private bool _configsDirty = true;
    private bool _statusesDirty = true;
    private long _nextFlushAtMs;

    public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
    {
        switch (evt)
        {
            case WiredVariableFxBoxChangedEvent:
            case WiredVariableBoxChangedEvent:
                _configsDirty = true;
                break;
            case RoomItemDetachedEvent detached:
                _furniReadyAtMs.Remove(detached.ObjectId);
                // The item may have been a fx box, or the variable box one stood on.
                _configsDirty = true;
                break;
            case RoomItemPlacedEvent placed:
                _furniReadyAtMs[placed.ObjectId] = ReadyAt();
                _statusesDirty = true;
                break;
            case PlayerEnterEvent entered:
                _playerReadyAtMs[entered.PlayerId] = ReadyAt();
                _statusesDirty = true;
                break;
            case PlayerLeftEvent left:
                _playerReadyAtMs.Remove(left.PlayerId);
                _viewersByPlayerId.Remove(left.PlayerId);
                _statusesDirty = true;
                break;
            case WiredVariableChangedEvent changed:
                if (
                    changed.TargetType
                    is WiredVariableTargetType.User
                        or WiredVariableTargetType.Furni
                )
                    _written.Add(
                        (
                            changed.VariableId,
                            changed.TargetType == WiredVariableTargetType.User,
                            changed.TargetId
                        )
                    );

                _statusesDirty = true;
                break;
            case GameTeamChangedEvent:
                _statusesDirty = true;
                break;
        }

        return Task.CompletedTask;
    }

    public void EnsureCanPlace(IRoomItem item)
    {
        if (item.Logic is not FurnitureWiredVariableFxLogic)
            return;

        var placed = FurniModule.Items.Count(x => x.Logic is FurnitureWiredVariableFxLogic);

        if (placed >= WiredConfig.VariableFxMaxBoxes)
            throw new TurboException(TurboErrorCodeEnum.WiredVariableFxLimitReached);
    }

    public Task ProcessAsync(long now, CancellationToken ct)
    {
        if (now < _nextFlushAtMs)
            return Task.CompletedTask;

        _nextFlushAtMs = now + WiredConfig.VariableFxFlushMs;

        if (_configsDirty)
            RebuildBindings();

        // A room without fx boxes has nothing to show and nobody to keep up to date.
        if (_bindingsByConfigId.Count == 0 && _viewersByPlayerId.Count == 0)
        {
            _statusesDirty = false;

            return Task.CompletedTask;
        }

        if (_statusesDirty)
            Flush(now);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Finds the fx boxes again and tells everyone who already has the configs what changed.
    /// A box the wired system has not loaded yet reads as defaults, so it waits a round.
    /// </summary>
    private void RebuildBindings()
    {
        _configsDirty = false;

        var found = new Dictionary<int, VariableFxBinding>();
        var countsByVariable = new Dictionary<RoomObjectId, int>();
        var perVariable = WiredConfig.VariableFxMaxPerVariable;

        // The fx an import add-on takes over count as boxes of this room, under their own ids.
        var boxes = FurniModule.Items.SelectMany<IRoomItem, FurnitureWiredVariableFxLogic>(item =>
            item.Logic switch
            {
                FurnitureWiredVariableFxLogic box => [box],
                WiredVariableReference reference =>
                    reference.ImportedAddons.OfType<FurnitureWiredVariableFxLogic>(),
                _ => [],
            }
        );

        foreach (var box in boxes.OrderBy(x => x.ObjectId))
        {
            if (!box.IsLoaded)
            {
                _configsDirty = true;

                continue;
            }

            if (found.Count >= WiredConfig.VariableFxMaxBoxes)
                break;

            // Past its variable box's limit an fx is kept, and its config sent, but not drawn;
            // the oldest fx on a variable box (lowest id) are the ones drawn (inferred).
            var overLimit = false;

            if (perVariable > 0 && box.GetShownVariable() is { } variable)
            {
                var count = countsByVariable.GetValueOrDefault(variable.ObjectId) + 1;

                countsByVariable[variable.ObjectId] = count;
                overLimit = count > perVariable;
            }

            found[box.ObjectId] = new VariableFxBinding(box, box.BuildConfig(), overLimit);
        }

        foreach (var binding in found.Values.Where(x => x.OverLimit))
        {
            if (!_overLimitIds.Contains(binding.Box.ObjectId))
                WiredSystem.RecordError(WiredErrorKind.VariableFxLimit, NowMs());
        }

        _overLimitIds.Clear();
        _overLimitIds.UnionWith(found.Values.Where(x => x.OverLimit).Select(x => x.Box.ObjectId));

        var removed = _bindingsByConfigId
            .Keys.Where(id => !found.ContainsKey(id))
            .ToImmutableArray();
        var changed = found
            .Values.Where(x =>
                !_bindingsByConfigId.TryGetValue(x.Config.ConfigId, out var known)
                || known.Signature != x.Signature
            )
            .Select(x => x.Config)
            .ToImmutableArray();

        // Habbo sends a placed fx's first statuses as a sync (allInitialize and isInitialize),
        // not as a change (official client capture 2026-10-24, wired matrix addon.1200.R2, 2/2).
        _newConfigIds.UnionWith(found.Keys.Where(id => !_bindingsByConfigId.ContainsKey(id)));
        _bindingsByConfigId.Clear();

        foreach (var (configId, binding) in found)
            _bindingsByConfigId[configId] = binding;

        _statusesDirty = true;

        if (removed.Length == 0 && changed.Length == 0)
            return;

        foreach (var viewer in _viewersByPlayerId.Values)
        {
            if (removed.Length > 0)
                viewer.Outbox.Add(
                    new VariableFxConfigsRemovedMessageComposer { ConfigIds = removed }
                );

            if (changed.Length > 0)
                viewer.Outbox.Add(new VariableFxConfigsMessageComposer { Configs = changed });
        }
    }

    private void Flush(long now)
    {
        var waiting = false;
        var newPlayers = TakeReady(_playerReadyAtMs, now, ref waiting);
        var newFurni = TakeReady(_furniReadyAtMs, now, ref waiting);

        // Someone is still inside their entry delay: look again next round.
        _statusesDirty = waiting;

        var players = AvatarModule
            .Players.Where(x => !_playerReadyAtMs.ContainsKey(x.PlayerId))
            .ToList();

        foreach (var player in players)
        {
            if (_viewersByPlayerId.ContainsKey(player.PlayerId))
                continue;

            var viewer = new VariableFxViewer();

            // Configs first: a status for a config the client does not have draws nothing.
            if (_bindingsByConfigId.Count > 0)
                viewer.Outbox.Add(
                    new VariableFxConfigsMessageComposer
                    {
                        Configs = [.. _bindingsByConfigId.Values.Select(x => x.Config)],
                    }
                );

            _viewersByPlayerId[player.PlayerId] = viewer;
        }

        // Each viewer's wanted statuses are rebuilt in the dictionary the viewer keeps, and the
        // viewers are looked up once here rather than once per status handed to them.
        _flushViewers.Clear();

        foreach (var player in players)
        {
            var viewer = _viewersByPlayerId[player.PlayerId];

            viewer.Wanted.Clear();
            _flushViewers.Add((player, viewer));
        }

        foreach (var binding in _bindingsByConfigId.Values)
            Collect(binding, players, newPlayers, newFurni);

        foreach (var (player, viewer) in _flushViewers)
            Send(player.PlayerId, viewer);

        _newConfigIds.Clear();
        _written.Clear();
    }

    /// <summary>Every holder of one fx's variable, handed to the viewers allowed to see it.</summary>
    private void Collect(
        VariableFxBinding binding,
        List<IRoomPlayer> players,
        HashSet<PlayerId> newPlayers,
        HashSet<int> newFurni
    )
    {
        var box = binding.Box;

        if (binding.OverLimit || box.GetShownVariable() is not { } variable)
            return;

        var variableId = variable.GetVarSnapshot().VariableId;
        var audience = GetAudience(box, players);
        var cap = WiredConfig.VariableFxMaxStatusesPerViewer;

        var isNew = _newConfigIds.Contains(binding.Config.ConfigId);

        void Hand(VariableFxStatusSnapshot status, IRoomPlayer? holder)
        {
            // What the status says is the same for every viewer, so it is signed once.
            var wanted = new VariableFxWantedStatus(
                status,
                VariableFxBinding.SignatureOf(status),
                _written.Contains((variableId, status.Key.IsUserEntity, status.Key.EntityId))
            );

            foreach (var (player, viewer) in _flushViewers)
            {
                if (!CanSee(box, audience, player, holder))
                    continue;

                if (viewer.Wanted.Count < cap)
                    viewer.Wanted[status.Key] = wanted;
            }
        }

        if (box.IsUserFx)
        {
            foreach (var holder in players)
            {
                var key = new WiredVariableKey(
                    variableId,
                    WiredVariableTargetType.User,
                    holder.ObjectId
                );

                if (!variable.TryGetValue(key, out var value))
                    continue;

                var teamColor = VariableFxStyles.GetTeamColor(GameSystem.GetTeam(holder.ObjectId));
                var status = box.ResolveStatus(
                    new VariableFxStatusKeySnapshot(
                        binding.Config.ConfigId,
                        variableId,
                        true,
                        holder.ObjectId
                    ),
                    holder.PlayerId,
                    value.Value,
                    teamColor
                );

                Hand(
                    status with
                    {
                        IsInitialize = isNew || newPlayers.Contains(holder.PlayerId),
                    },
                    holder
                );
            }

            return;
        }

        // The client can only draw over floor furni.
        foreach (var item in FurniModule.Items)
        {
            if (item is not IRoomFloorItem || _furniReadyAtMs.ContainsKey(item.ObjectId))
                continue;

            var key = new WiredVariableKey(
                variableId,
                WiredVariableTargetType.Furni,
                item.ObjectId
            );

            if (!variable.TryGetValue(key, out var value))
                continue;

            var status = box.ResolveStatus(
                new VariableFxStatusKeySnapshot(
                    binding.Config.ConfigId,
                    variableId,
                    false,
                    item.ObjectId
                ),
                item.ObjectId,
                value.Value,
                teamColor: null
            );

            Hand(
                status with
                {
                    IsInitialize = isNew || newFurni.Contains(item.ObjectId),
                },
                holder: null
            );
        }
    }

    /// <summary>
    /// The viewers a "has variable" audience lets in, worked out once per fx; null for the
    /// audiences that depend on nothing or on the holder.
    /// </summary>
    private static HashSet<PlayerId>? GetAudience(
        FurnitureWiredVariableFxLogic box,
        List<IRoomPlayer> players
    )
    {
        var visibility = box.Visibility;

        if (
            visibility
            is not (
                VariableFxVisibilityType.HasVariable
                or VariableFxVisibilityType.HasVariableWithValue
            )
        )
            return null;

        var audience = new HashSet<PlayerId>();

        // With no variable chosen there is nothing a viewer could have, so nobody sees it.
        if (box.GetAudienceVariable() is not { } variable)
            return audience;

        var variableId = variable.GetVarSnapshot().VariableId;

        foreach (var viewer in players)
        {
            var key = new WiredVariableKey(
                variableId,
                WiredVariableTargetType.User,
                viewer.ObjectId
            );

            if (!variable.TryGetValue(key, out var value))
                continue;

            if (
                visibility == VariableFxVisibilityType.HasVariable
                || value.Value == box.AudienceValue
            )
                audience.Add(viewer.PlayerId);
        }

        return audience;
    }

    private bool CanSee(
        FurnitureWiredVariableFxLogic box,
        HashSet<PlayerId>? audience,
        IRoomPlayer viewer,
        IRoomPlayer? holder
    )
    {
        if (audience is not null)
            return audience.Contains(viewer.PlayerId);

        switch (box.Visibility)
        {
            case VariableFxVisibilityType.OnlyUser:
                return holder?.PlayerId == viewer.PlayerId;
            case VariableFxVisibilityType.GameTeam:
            {
                if (holder is null || holder.PlayerId == viewer.PlayerId)
                    return holder is not null;

                var team = GameSystem.GetTeam(holder.ObjectId);

                return team != GameTeamType.None && team == GameSystem.GetTeam(viewer.ObjectId);
            }
            default:
                return true;
        }
    }

    /// <summary>Sends one viewer what differs from what they were last sent, as one ordered batch.</summary>
    private void Send(PlayerId playerId, VariableFxViewer viewer)
    {
        var wanted = viewer.Wanted;
        var gone = viewer.Sent.Keys.Where(key => !wanted.ContainsKey(key)).ToImmutableArray();
        var updates = ImmutableArray.CreateBuilder<VariableFxStatusSnapshot>();

        foreach (var (key, (status, signature, written)) in wanted)
        {
            // A value written again unchanged is shown again too, as Habbo does: Give Variable
            // 50, then set 30, 30, 80 sent one VariableFxStatus each, the second 30 included
            // (official client capture 2026-10-24, wired matrix addon.1200.R1, 4/4).
            if (!written && viewer.Sent.TryGetValue(key, out var sent) && sent == signature)
                continue;

            viewer.Sent[key] = signature;
            updates.Add(status);
        }

        foreach (var key in gone)
            viewer.Sent.Remove(key);

        if (gone.Length > 0)
            viewer.Outbox.Add(new VariableFxStatusRemovedMessageComposer { Keys = gone });

        if (updates.Count > 0)
            viewer.Outbox.Add(
                new VariableFxStatusMessageComposer
                {
                    // A viewer's first batch brings them up to date, as does a batch of only
                    // placed fx; nothing in it is news.
                    InitializeAll = !viewer.IsSynced || updates.All(x => x.IsInitialize),
                    Statuses = updates.ToImmutable(),
                }
            );

        viewer.IsSynced = true;

        if (viewer.Outbox.Count == 0)
            return;

        IReadOnlyList<IComposer> batch = [.. viewer.Outbox];

        viewer.Outbox.Clear();

        // Told, not awaited: this runs in the room tick, and the presence may be waiting on the
        // room. One call per viewer keeps configs ahead of the statuses that need them.
        _roomGrain
            ._grainFactory.GetPlayerPresenceGrain(playerId)
            .SendComposerAsync(batch, CancellationToken.None)
            .LogAndForget(
                Logger,
                "send variable fx to player {PlayerId} in room {RoomId}",
                playerId,
                _roomGrain.RoomId
            );
    }

    private long ReadyAt() => NowMs() + WiredConfig.VariableFxEntryDelayMs;

    /// <summary>Takes out the entries whose delay is over; says whether any are still waiting.</summary>
    private static HashSet<TKey> TakeReady<TKey>(
        Dictionary<TKey, long> readyAtMs,
        long now,
        ref bool waiting
    )
        where TKey : notnull
    {
        var ready = readyAtMs.Where(x => x.Value <= now).Select(x => x.Key).ToHashSet();

        foreach (var key in ready)
            readyAtMs.Remove(key);

        waiting |= readyAtMs.Count > 0;

        return ready;
    }
}
