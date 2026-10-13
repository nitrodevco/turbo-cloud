using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Logging;
using Turbo.Primitives.Messages.Incoming.Userdefinedroomevents;
using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Events.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Leaderboard;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Grains;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Addons;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Variables;

namespace Turbo.Rooms.Wired.Leaderboard;

/// <summary>
/// The room's WIRED leaderboards (Flash <c>roomevents/variable_leaderboard/VariableLeaderboardController.as</c>).
/// <para>
/// A viewer entering the room sends <c>VariableLeaderboardReplaceSubscription</c> (0, 0, "0", [])
/// and gets every published leaderboard back in one <c>VariableLeaderboardSnapshot</c>, empty when
/// there is none (official client capture, WIN63-202610121709). For each leaderboard it shows, it
/// then subscribes to the one or two chunks of 50 rows its page needs, for the query revision and
/// variable it shows, and is sent those chunks. A subscription replaces the viewer's previous one
/// for that leaderboard. When a leaderboard changes it gets a new query revision and the viewers
/// get a new snapshot (they subscribe again); when a value changes the chunks a viewer holds are
/// worked out again and the ones that differ are sent.
/// </para>
/// </summary>
public sealed class RoomVariableLeaderboardSystem(RoomGrain roomGrain)
    : RoomGrainComponent(roomGrain),
        IRoomEventListener
{
    public const int MAX_OVERLAYS = 4;
    public const int CHUNK_SIZE = 50;

    private readonly List<Overlay> _overlays = [];
    private readonly HashSet<PlayerId> _viewers = [];
    private readonly Dictionary<(PlayerId, int), Subscription> _subscriptions = [];
    private readonly Dictionary<int, (string Name, string Figure)> _absentPlayers = [];

    // Overlay ids by add-on: Habbo numbers a room's leaderboards from 1 (official client capture
    // 2026-10-13: the first one published in a room is overlay 1, revision 1, not its furni id).
    private readonly Dictionary<int, int> _overlayIdsByBox = [];
    private int _lastOverlayId;

    private bool _overlaysDirty = true;
    private bool _rowsDirty;
    private int _lastRevision;
    private long _nextFlushAtMs;

    public Task OnRoomEventAsync(RoomEvent evt, CancellationToken ct)
    {
        switch (evt)
        {
            case RoomWiredStackChangedEvent:
            case WiredVariableBoxChangedEvent:
            case RoomItemDetachedEvent:
            case RoomItemPlacedEvent:
            case RoomItemMovedEvent:
                _overlaysDirty = true;
                break;
            case WiredVariableChangedEvent:
                _rowsDirty = true;
                break;
            case PlayerLeftEvent left:
                Forget(left.PlayerId);
                break;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// A viewer's <c>VariableLeaderboardReplaceSubscription</c>: what to send them now. The room
    /// open form forgets their subscriptions and answers with the snapshot; any other one
    /// replaces their subscription to that leaderboard and answers with its chunks. One for a
    /// leaderboard, revision or variable the room does not have is dropped, as the client drops
    /// a chunk it did not ask for: a snapshot telling them what is current is on its way.
    /// </summary>
    public async Task<ImmutableArray<IComposer>> ReplaceSubscriptionAsync(
        PlayerId playerId,
        VariableLeaderboardReplaceSubscriptionMessage message,
        CancellationToken ct
    )
    {
        if (!IsValid(message))
            return [];

        if (_overlaysDirty)
            RebuildOverlays();

        if (message.ChunkIndices.Length == 0)
        {
            Forget(playerId);
            _viewers.Add(playerId);

            return [BuildSnapshot()];
        }

        var overlay = _overlays.FirstOrDefault(x =>
            x.Descriptor.OverlayInstanceId == message.OverlayInstanceId
        );

        if (
            overlay is null
            || overlay.Descriptor.QueryRevision != message.QueryRevision
            || !overlay.Descriptor.Choices.Any(x => x.VariableId == message.SelectedVariableId)
        )
            return [];

        var subscription = new Subscription(
            overlay.Descriptor.OverlayInstanceId,
            overlay.Descriptor.QueryRevision,
            message.SelectedVariableId,
            [.. message.ChunkIndices]
        );

        _subscriptions[(playerId, subscription.OverlayInstanceId)] = subscription;

        var rows = await BuildRowsAsync(overlay, subscription.VariableId, ct);
        var replies = ImmutableArray.CreateBuilder<IComposer>();

        foreach (var chunkIndex in subscription.ChunkIndices)
        {
            var chunk = BuildChunk(subscription, rows, chunkIndex);

            subscription.Sent[chunkIndex] = Signature(chunk);
            replies.Add(chunk);
        }

        return replies.ToImmutable();
    }

    /// <summary>
    /// The overlay a published add-on is shown as, for its editor (context section 8); 0 while it
    /// is unpublished, as Habbo sends it.
    /// </summary>
    public int GetOverlayInstanceId(WiredAddonVariableLeaderboard box)
    {
        if (!box.IsPublished)
            return 0;

        if (_overlaysDirty || !_overlays.Any(x => x.Box == box))
            RebuildOverlays();

        return _overlays.FirstOrDefault(x => x.Box == box)?.Descriptor.OverlayInstanceId ?? 0;
    }

    public async Task ProcessAsync(long now, CancellationToken ct)
    {
        if (now < _nextFlushAtMs || (!_overlaysDirty && !_rowsDirty))
            return;

        _nextFlushAtMs = now + _roomGrain._wiredConfig.VariableLeaderboardFlushMs;

        if (_overlaysDirty && RebuildOverlays() && _viewers.Count > 0)
            Send(_viewers, BuildSnapshot());

        if (!_rowsDirty)
            return;

        _rowsDirty = false;

        foreach (
            var group in _subscriptions.GroupBy(x =>
                (x.Value.OverlayInstanceId, x.Value.VariableId)
            )
        )
        {
            var overlay = _overlays.FirstOrDefault(x =>
                x.Descriptor.OverlayInstanceId == group.Key.OverlayInstanceId
            );

            if (overlay is null)
                continue;

            var rows = await BuildRowsAsync(overlay, group.Key.VariableId, ct);

            foreach (var ((playerId, _), subscription) in group)
            {
                foreach (var chunkIndex in subscription.ChunkIndices)
                {
                    var chunk = BuildChunk(subscription, rows, chunkIndex);
                    var signature = Signature(chunk);

                    if (
                        subscription.Sent.TryGetValue(chunkIndex, out var sent)
                        && sent == signature
                    )
                        continue;

                    subscription.Sent[chunkIndex] = signature;
                    Send([playerId], chunk);
                }
            }
        }
    }

    /// <summary>
    /// What Flash's composer allows: up to two distinct, non-negative chunks, and none only in the
    /// room open form (<c>VariableLeaderboardReplaceSubscriptionMessageComposer</c> constructor).
    /// </summary>
    private static bool IsValid(VariableLeaderboardReplaceSubscriptionMessage message)
    {
        var chunks = message.ChunkIndices;

        if (chunks.Length > 2 || chunks.Any(x => x < 0))
            return false;

        if (chunks.Length == 2 && chunks[0] == chunks[1])
            return false;

        return chunks.Length > 0
            || (
                message.OverlayInstanceId == 0
                && message.QueryRevision == 0
                && message.SelectedVariableId == VariableLeaderboardConfig.NULL_VARIABLE_ID
            );
    }

    private void Forget(PlayerId playerId)
    {
        _viewers.Remove(playerId);

        foreach (var key in _subscriptions.Keys.Where(x => x.Item1 == playerId).ToList())
            _subscriptions.Remove(key);
    }

    /// <summary>
    /// Finds the published leaderboards again, at most four in placement order. One whose
    /// descriptor changed gets a new query revision and loses its subscriptions. Says whether
    /// anything a viewer was told changed.
    /// </summary>
    private bool RebuildOverlays()
    {
        _overlaysDirty = false;

        var found = new List<Overlay>();

        foreach (var item in FurniModule.Items.OrderBy(x => x.ObjectId.Value))
        {
            if (item.Logic is not WiredAddonVariableLeaderboard box)
                continue;

            if (box.Config is null)
                continue;

            if (found.Count >= MAX_OVERLAYS)
                break;

            var known = _overlays.FirstOrDefault(x => x.Box == box);

            if (!_overlayIdsByBox.TryGetValue(box.ObjectId.Value, out var overlayId))
                _overlayIdsByBox[box.ObjectId.Value] = overlayId = ++_lastOverlayId;

            var descriptor = BuildDescriptor(
                box,
                box.Config,
                overlayId,
                known?.Descriptor.QueryRevision ?? 0
            );

            if (known is not null && Key(known.Descriptor) == Key(descriptor))
            {
                found.Add(known);

                continue;
            }

            descriptor = descriptor with { QueryRevision = ++_lastRevision };

            foreach (
                var key in _subscriptions
                    .Keys.Where(x => x.Item2 == descriptor.OverlayInstanceId)
                    .ToList()
            )
                _subscriptions.Remove(key);

            found.Add(new Overlay(box, descriptor));
        }

        var changed =
            found.Count != _overlays.Count
            || found.Where((overlay, i) => !ReferenceEquals(overlay, _overlays[i])).Any();

        foreach (var removed in _overlays.Where(x => !found.Contains(x)))
        {
            foreach (
                var key in _subscriptions
                    .Keys.Where(x => x.Item2 == removed.Descriptor.OverlayInstanceId)
                    .ToList()
            )
                _subscriptions.Remove(key);
        }

        _overlays.Clear();
        _overlays.AddRange(found);

        return changed;
    }

    private static VariableLeaderboardDescriptorSnapshot BuildDescriptor(
        WiredAddonVariableLeaderboard box,
        VariableLeaderboardConfig config,
        int overlayId,
        int revision
    )
    {
        var choices = box.GetChoices()
            .Select(variable =>
            {
                var snapshot = variable.GetVarSnapshot();
                var variableId = snapshot.VariableId.ToString();

                return new VariableLeaderboardChoiceSnapshot
                {
                    VariableId = variableId,
                    DisplayName =
                        config.DisplayOverrideFor(variableId)
                        ?? VariableLeaderboardConfig.SuggestDisplayName(snapshot.VariableName),
                };
            })
            .ToImmutableArray();

        return new VariableLeaderboardDescriptorSnapshot
        {
            OverlayInstanceId = overlayId,
            QueryRevision = revision,
            Title = config.Title,
            Description = config.Description,
            TileX = config.TileX,
            TileY = config.TileY,
            OffsetX = config.OffsetX,
            Altitude = config.Altitude,
            SortMode = config.SortMode,
            Columns = config.Columns,
            Choices = choices,
            DefaultVariableId =
                choices.Length == 0
                    ? VariableLeaderboardConfig.NULL_VARIABLE_ID
                    : choices[0].VariableId,
            PageSizeOption = config.PageSizeOption,
        };
    }

    /// <summary>Everything a viewer is told about a leaderboard but its revision.</summary>
    private static string Key(VariableLeaderboardDescriptorSnapshot descriptor) =>
        string.Join(
            '	',
            (descriptor with { QueryRevision = 0, Columns = [], Choices = [] }).ToString(),
            string.Join('|', descriptor.Columns),
            string.Join('|', descriptor.Choices)
        );

    private VariableLeaderboardSnapshotMessageComposer BuildSnapshot() =>
        new() { Descriptors = [.. _overlays.Select(x => x.Descriptor)] };

    /// <summary>
    /// Every holder of the variable the leaderboard shows, filtered and sorted as it is set up,
    /// ranked from 1. Values of players who are not in the room come from the variable's store.
    /// </summary>
    private async Task<List<VariableLeaderboardRowSnapshot>> BuildRowsAsync(
        Overlay overlay,
        string variableId,
        CancellationToken ct
    )
    {
        var box = overlay.Box;
        var config = box.Config;
        var variable = box.GetChoices()
            .FirstOrDefault(x => x.GetVarSnapshot().VariableId.ToString() == variableId);

        if (config is null || variable is null)
            return [];

        var snapshot = variable.GetVarSnapshot();
        var hasValue = snapshot.Flags.HasFlag(WiredVariableFlags.HasValue);
        var holders = variable.GetStoredUserValues().ToList();

        if (config.FilterEnabled)
            holders = Filter(config, holders);

        holders.Sort(
            (a, b) =>
            {
                var order = config.SortMode switch
                {
                    VariableLeaderboardConfig.SORT_HIGHEST_VALUE => b.Value.Value.CompareTo(
                        a.Value.Value
                    ),
                    VariableLeaderboardConfig.SORT_LOWEST_VALUE => a.Value.Value.CompareTo(
                        b.Value.Value
                    ),
                    VariableLeaderboardConfig.SORT_OLDEST_CREATION => a.CreatedAtMs.CompareTo(
                        b.CreatedAtMs
                    ),
                    VariableLeaderboardConfig.SORT_LATEST_CREATION => b.CreatedAtMs.CompareTo(
                        a.CreatedAtMs
                    ),
                    VariableLeaderboardConfig.SORT_OLDEST_UPDATE => a.UpdatedAtMs.CompareTo(
                        b.UpdatedAtMs
                    ),
                    _ => b.UpdatedAtMs.CompareTo(a.UpdatedAtMs),
                };

                // Ties: who got there first, then the lower user id (this server's choice).
                return order != 0 ? order
                    : a.UpdatedAtMs != b.UpdatedAtMs ? a.UpdatedAtMs.CompareTo(b.UpdatedAtMs)
                    : a.PlayerId.CompareTo(b.PlayerId);
            }
        );

        var levelUp = config.HasColumn(VariableLeaderboardConfig.COLUMN_LEVEL)
            ? box.GetLevelUps(variable).FirstOrDefault()
            : null;
        var textConnected =
            config.HasColumn(VariableLeaderboardConfig.COLUMN_TEXT)
            && snapshot.Flags.HasFlag(WiredVariableFlags.HasTextConnector);
        var rows = new List<VariableLeaderboardRowSnapshot>(holders.Count);

        foreach (var holder in holders)
        {
            var (name, figure) = await GetPlayerAsync(holder.PlayerId, ct);
            string? level = null;

            if (
                levelUp is not null
                && levelUp.TryGetLevelProgress(holder.Value.Value, out var progress)
            )
                level = progress.Level.ToString(CultureInfo.InvariantCulture);

            rows.Add(
                new VariableLeaderboardRowSnapshot
                {
                    Rank = rows.Count + 1,
                    UserId = holder.PlayerId,
                    Username = name,
                    Figure = figure,
                    RawValue = hasValue
                        ? holder.Value.Value.ToString(CultureInfo.InvariantCulture)
                        : string.Empty,
                    CreationTime = holder.CreatedAtMs,
                    LastUpdateTime = holder.UpdatedAtMs,
                    Level = level,
                    TextConnection = textConnected
                        ? snapshot.TextConnectors.TryGetValue(holder.Value, out var label)
                            ? label
                            : snapshot.TextConnectorFallback
                                ?? holder.Value.Value.ToString(CultureInfo.InvariantCulture)
                        : null,
                }
            );
        }

        return rows;
    }

    /// <summary>
    /// The holders that also hold the filtering variable, and, with the comparison on, whose
    /// filtering value equals the literal or the global variable's value. Equality is this
    /// server's reading: the editor offers no operator.
    /// </summary>
    private List<(
        int PlayerId,
        WiredVariableValue Value,
        long CreatedAtMs,
        long UpdatedAtMs
    )> Filter(
        VariableLeaderboardConfig config,
        List<(int PlayerId, WiredVariableValue Value, long CreatedAtMs, long UpdatedAtMs)> holders
    )
    {
        if (
            !WiredVariableId.TryParse(config.FilterVariableId, out var filterId)
            || WiredSystem.GetVariableById(filterId) is not FurnitureWiredVariableLogic filter
        )
            return [];

        var filterValues = filter.GetStoredUserValues().ToDictionary(x => x.PlayerId, x => x.Value);
        long? operand = null;

        if (config.ComparisonEnabled)
        {
            if (config.OperandKind == VariableLeaderboardConfig.OPERAND_LITERAL)
                operand = long.Parse(config.Literal, CultureInfo.InvariantCulture);
            else if (
                WiredVariableId.TryParse(config.GlobalOperandVariableId, out var globalId)
                && WiredSystem.GetVariableById(globalId) is { } global
                && global.TryGetValue(
                    new WiredVariableKey(globalId, WiredVariableTargetType.Global, 0),
                    out var globalValue
                )
            )
                operand = globalValue.Value;
            else
                return [];
        }

        return
        [
            .. holders.Where(holder =>
                filterValues.TryGetValue(holder.PlayerId, out var value)
                && (operand is null || value.Value == operand)
            ),
        ];
    }

    private async Task<(string Name, string Figure)> GetPlayerAsync(
        int playerId,
        CancellationToken ct
    )
    {
        if (AvatarModule.TryGetPlayer(new PlayerId(playerId), out var player))
            return (player.Name, player.Figure);

        if (_absentPlayers.TryGetValue(playerId, out var known))
            return known;

        try
        {
            var summary = await _roomGrain
                ._grainFactory.GetPlayerGrain(new PlayerId(playerId))
                .GetSummaryAsync(ct);

            known = summary is null ? (string.Empty, string.Empty) : (summary.Name, summary.Figure);
        }
        catch (Exception)
        {
            known = (string.Empty, string.Empty);
        }

        _absentPlayers[playerId] = known;

        return known;
    }

    private static VariableLeaderboardChunkUpdateMessageComposer BuildChunk(
        Subscription subscription,
        List<VariableLeaderboardRowSnapshot> rows,
        int chunkIndex
    ) =>
        new()
        {
            OverlayInstanceId = subscription.OverlayInstanceId,
            QueryRevision = subscription.QueryRevision,
            SelectedVariableId = subscription.VariableId,
            ChunkIndex = chunkIndex,
            TotalCount = rows.Count,
            Rows = [.. rows.Skip(chunkIndex * CHUNK_SIZE).Take(CHUNK_SIZE)],
        };

    private static string Signature(VariableLeaderboardChunkUpdateMessageComposer chunk) =>
        string.Join(
            '\n',
            chunk
                .Rows.Select(row => row.ToString())
                .Prepend(chunk.TotalCount.ToString(CultureInfo.InvariantCulture))
        );

    private void Send(IEnumerable<PlayerId> playerIds, IComposer composer)
    {
        foreach (var playerId in playerIds.ToList())
            _roomGrain
                ._grainFactory.SendComposerToPlayerAsync(playerId, composer, CancellationToken.None)
                .LogAndForget(
                    _roomGrain._logger,
                    "send a variable leaderboard to player {PlayerId} in room {RoomId}",
                    playerId,
                    _roomGrain.RoomId
                );
    }

    private sealed record Overlay(
        WiredAddonVariableLeaderboard Box,
        VariableLeaderboardDescriptorSnapshot Descriptor
    );

    private sealed record Subscription(
        int OverlayInstanceId,
        int QueryRevision,
        string VariableId,
        ImmutableArray<int> ChunkIndices
    )
    {
        public Dictionary<int, string> Sent { get; } = [];
    }
}
