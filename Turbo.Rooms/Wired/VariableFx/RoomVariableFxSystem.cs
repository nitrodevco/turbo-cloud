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
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Snapshots.Wired.VariableFx;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons.VariableFx;

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
    : IRoomEventListener,
        IRoomPlacementLimit
{
    private readonly RoomGrain _roomGrain = roomGrain;

    private readonly Dictionary<int, VariableFxBinding> _bindingsByConfigId = [];
    private readonly Dictionary<PlayerId, VariableFxViewer> _viewersByPlayerId = [];

    // Players, and the avatars and furni, the client is still being told about: when each may
    // first be sent or shown.
    private readonly Dictionary<PlayerId, long> _playerReadyAtMs = [];
    private readonly Dictionary<int, long> _furniReadyAtMs = [];

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
            case WiredVariableChangedEvent:
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

        var placed = _roomGrain.FurniModule.Items.Count(x =>
            x.Logic is FurnitureWiredVariableFxLogic
        );

        if (placed >= _roomGrain._wiredConfig.VariableFxMaxBoxes)
            throw new TurboException(TurboErrorCodeEnum.WiredVariableFxLimitReached);
    }

    public Task ProcessAsync(long now, CancellationToken ct)
    {
        if (now < _nextFlushAtMs)
            return Task.CompletedTask;

        _nextFlushAtMs = now + _roomGrain._wiredConfig.VariableFxFlushMs;

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

        foreach (var item in _roomGrain.FurniModule.Items)
        {
            if (item.Logic is not FurnitureWiredVariableFxLogic box)
                continue;

            if (!box.IsLoaded)
            {
                _configsDirty = true;

                continue;
            }

            if (found.Count >= _roomGrain._wiredConfig.VariableFxMaxBoxes)
                break;

            found[item.ObjectId] = new VariableFxBinding(box, box.BuildConfig());
        }

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

        var players = _roomGrain
            .AvatarModule.Players.Where(x => !_playerReadyAtMs.ContainsKey(x.PlayerId))
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

        var wanted = players.ToDictionary(
            x => x.PlayerId,
            _ => new Dictionary<VariableFxStatusKeySnapshot, VariableFxStatusSnapshot>()
        );

        foreach (var binding in _bindingsByConfigId.Values)
            Collect(binding, players, newPlayers, newFurni, wanted);

        foreach (var player in players)
            Send(player.PlayerId, _viewersByPlayerId[player.PlayerId], wanted[player.PlayerId]);
    }

    /// <summary>Every holder of one fx's variable, handed to the viewers allowed to see it.</summary>
    private void Collect(
        VariableFxBinding binding,
        List<IRoomPlayer> players,
        HashSet<PlayerId> newPlayers,
        HashSet<int> newFurni,
        Dictionary<
            PlayerId,
            Dictionary<VariableFxStatusKeySnapshot, VariableFxStatusSnapshot>
        > wanted
    )
    {
        var box = binding.Box;

        if (box.GetShownVariable() is not { } variable)
            return;

        var variableId = variable.GetVarSnapshot().VariableId;
        var audience = GetAudience(box, players);
        var cap = _roomGrain._wiredConfig.VariableFxMaxStatusesPerViewer;

        void Hand(VariableFxStatusSnapshot status, IRoomPlayer? holder)
        {
            foreach (var viewer in players)
            {
                if (!CanSee(box, audience, viewer, holder))
                    continue;

                var statuses = wanted[viewer.PlayerId];

                if (statuses.Count < cap)
                    statuses[status.Key] = status;
            }
        }

        if (box.IsUserFx)
        {
            foreach (var holder in players)
            {
                var key = new WiredVariableKey(
                    variableId,
                    WiredVariableTargetType.User,
                    holder.PlayerId
                );

                if (!variable.TryGetValue(key, out var value))
                    continue;

                var teamColor = VariableFxStyles.GetTeamColor(
                    _roomGrain.GameSystem.GetTeam(holder.PlayerId)
                );
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

                Hand(status with { IsInitialize = newPlayers.Contains(holder.PlayerId) }, holder);
            }

            return;
        }

        // The client can only draw over floor furni.
        foreach (var item in _roomGrain.FurniModule.Items)
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

            Hand(status with { IsInitialize = newFurni.Contains(item.ObjectId) }, holder: null);
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
                viewer.PlayerId
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

                var team = _roomGrain.GameSystem.GetTeam(holder.PlayerId);

                return team != GameTeamType.None
                    && team == _roomGrain.GameSystem.GetTeam(viewer.PlayerId);
            }
            default:
                return true;
        }
    }

    /// <summary>Sends one viewer what differs from what they were last sent, as one ordered batch.</summary>
    private void Send(
        PlayerId playerId,
        VariableFxViewer viewer,
        Dictionary<VariableFxStatusKeySnapshot, VariableFxStatusSnapshot> wanted
    )
    {
        var gone = viewer.Sent.Keys.Where(key => !wanted.ContainsKey(key)).ToImmutableArray();
        var updates = ImmutableArray.CreateBuilder<VariableFxStatusSnapshot>();

        foreach (var (key, status) in wanted)
        {
            var signature = VariableFxBinding.SignatureOf(status);

            if (viewer.Sent.TryGetValue(key, out var sent) && sent == signature)
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
                    // A viewer's first batch brings them up to date; nothing in it is news.
                    InitializeAll = !viewer.IsSynced,
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
                _roomGrain._logger,
                $"send variable fx to player {playerId} in room {_roomGrain.RoomId}"
            );
    }

    private long ReadyAt() => _roomGrain.NowMs() + _roomGrain._wiredConfig.VariableFxEntryDelayMs;

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
