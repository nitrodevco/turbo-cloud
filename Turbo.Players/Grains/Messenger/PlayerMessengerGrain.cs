using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Messenger;
using Turbo.Players.Configuration;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums.Messenger;
using Turbo.Primitives.Players.Grains.Messenger;
using Turbo.Primitives.Players.Messenger;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Messenger;

namespace Turbo.Players.Grains.Messenger;

/// <summary>
/// Owns a player's friend list, requests, ignore list and console conversations. Friend and
/// request mutations write through to the database as they happen; only the delivered flags of
/// received messages are buffered, and those are flushed on a timer and on deactivation so a
/// busy conversation does not issue one write per message.
///
/// Two friends act on each other all the time — both message, both log in, both accept — so
/// every call one messenger makes on another is an interleaved tell that touches memory only
/// (<c>On*</c>, <c>Receive*</c>, <c>CanBeAddedBy</c>). The side that starts a change does the
/// database work for both; the other side is only told.
/// </summary>
internal sealed class PlayerMessengerGrain : Grain, IPlayerMessengerGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly PlayerConfig _playerConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IPlayerMessengerGrain> _logger;

    private readonly PlayerMessengerLiveState _state;

    private IDisposable? _deliveredFlushTimer;

    public PlayerMessengerGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<PlayerConfig> playerConfig,
        IGrainFactory grainFactory,
        ILogger<IPlayerMessengerGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _playerConfig = playerConfig.Value;
        _grainFactory = grainFactory;
        _logger = logger;

        _state = new() { PlayerId = this.GetPlayerId() };
    }

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        try
        {
            await HydrateFromDatabaseAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to hydrate messenger for player {PlayerId}",
                _state.PlayerId
            );

            throw;
        }

        _deliveredFlushTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) =>
                await ((PlayerMessengerGrain)self!).FlushDeliveredMessagesAsync(ct),
            this,
            TimeSpan.FromMilliseconds(_playerConfig.MessengerDeliveredFlushMs),
            TimeSpan.FromMilliseconds(_playerConfig.MessengerDeliveredFlushMs)
        );
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        _deliveredFlushTimer?.Dispose();
        _deliveredFlushTimer = null;

        await FlushDeliveredMessagesAsync(ct);
    }

    public Task<FriendListErrorCodeType> CanBeAddedByAsync(PlayerId playerId, CancellationToken ct)
    {
        // Answered from memory, between the accepting side's other awaits, so two accepts in
        // the same instant can each see one free slot. One friend over the limit is the worst
        // case, and the alternative is the two grains waiting on each other.
        if (_state.Friends.Count >= GetFriendLimit())
            return Task.FromResult(FriendListErrorCodeType.TheyHitFriendLimit);

        if (_state.BlockedPlayerIds.Contains(playerId))
            return Task.FromResult(FriendListErrorCodeType.BlockedByThem);

        return Task.FromResult(FriendListErrorCodeType.None);
    }

    public Task OnFriendAddedAsync(PlayerSummarySnapshot snapshot, CancellationToken ct)
    {
        AddFriendToState(snapshot);

        return FlushUpdatesAsync(ct);
    }

    /// <summary>
    /// Writes a friendship from both sides, and drops any request either of them had open, in
    /// one save: two saves on two grains could leave one of them friends with somebody who is
    /// not friends with them.
    /// </summary>
    private async Task AddFriendshipAsync(PlayerSummarySnapshot friend, CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        await dbCtx
            .MessengerRequests.Where(x =>
                (
                    x.PlayerEntityId == friend.PlayerId.Value
                    && x.RequestedPlayerEntityId == _state.PlayerId.Value
                )
                || (
                    x.PlayerEntityId == _state.PlayerId.Value
                    && x.RequestedPlayerEntityId == friend.PlayerId.Value
                )
            )
            .ExecuteDeleteAsync(ct);

        dbCtx.Add(NewFriendRow(_state.PlayerId, friend.PlayerId));
        dbCtx.Add(NewFriendRow(friend.PlayerId, _state.PlayerId));

        await dbCtx.SaveChangesAsync(ct);

        AddFriendToState(friend);
    }

    private static MessengerFriendEntity NewFriendRow(PlayerId playerId, PlayerId friendId) =>
        new()
        {
            PlayerEntityId = playerId.Value,
            FriendPlayerEntityId = friendId.Value,
            RelationType = MessengerFriendRelationType.Zero,
            PlayerEntity = null!,
            FriendPlayerEntity = null!,
        };

    private void AddFriendToState(PlayerSummarySnapshot snapshot)
    {
        _state.Friends[snapshot.PlayerId] = MessengerFriendDto.FromSummary(snapshot);
        _state.IncomingRequests.Remove(snapshot.PlayerId);

        ForceUpdate(snapshot.PlayerId);
    }

    private FriendListErrorCodeType CanAddFriend(PlayerId playerId)
    {
        var errorCode = FriendListErrorCodeType.None;

        if (_state.Friends.Count >= GetFriendLimit())
            errorCode = FriendListErrorCodeType.YouHitFriendLimit;
        else if (!_state.IncomingRequests.TryGetValue(playerId, out var request))
            errorCode = FriendListErrorCodeType.FriendRequestNotFound;
        else if (_state.BlockedPlayerIds.Contains(playerId))
            errorCode = FriendListErrorCodeType.BlockedByYou;

        return errorCode;
    }

    public async Task RemoveFriendsAsync(List<PlayerId> playerIds, CancellationToken ct)
    {
        var validPlayerIds = playerIds
            .Where(x => _state.Friends.ContainsKey(x))
            .Select(x => (int)x)
            .ToList();

        if (validPlayerIds.Count == 0)
            return;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        await dbCtx
            .MessengerFriends.Where(x =>
                (
                    x.PlayerEntityId == _state.PlayerId.Value
                    && validPlayerIds.Contains(x.FriendPlayerEntityId)
                )
                || (
                    validPlayerIds.Contains(x.PlayerEntityId)
                    && x.FriendPlayerEntityId == _state.PlayerId.Value
                )
            )
            .ExecuteDeleteAsync(ct);

        foreach (var playerId in validPlayerIds)
            RemoveFriendFromState(playerId);

        // Each former friend is a grain of their own, so they are told side by side.
        await Task.WhenAll(
            validPlayerIds.Select(playerId =>
                _grainFactory
                    .GetPlayerMessengerGrain(playerId)
                    .OnFriendRemovedAsync(_state.PlayerId, ct)
            )
        );

        await FlushUpdatesAsync(ct);
    }

    public Task OnFriendRemovedAsync(PlayerId playerId, CancellationToken ct)
    {
        RemoveFriendFromState(playerId);

        return FlushUpdatesAsync(ct);
    }

    private void RemoveFriendFromState(PlayerId playerId)
    {
        if (_state.Friends.TryGetValue(playerId, out var friend))
        {
            _state.Friends.Remove(playerId);

            _state.PendingUpdates.Add(
                playerId,
                new MessengerUpdateSnapshot
                {
                    ActionType = FriendListUpdateActionType.Removed,
                    FriendId = playerId,
                }
            );
        }
    }

    public async Task<List<MessengerAcceptFriendFailure>> AcceptFriendRequestsAsync(
        List<int> playerIds,
        CancellationToken ct
    )
    {
        var failures = new List<MessengerAcceptFriendFailure>();
        var snapshot = await _grainFactory.GetPlayerGrain(_state.PlayerId).GetSummaryAsync(ct);

        foreach (var playerId in playerIds)
        {
            var errorCode = CanAddFriend(playerId);

            if (errorCode != FriendListErrorCodeType.None)
            {
                failures.Add(
                    new MessengerAcceptFriendFailure { ErrorCode = errorCode, SenderId = -1 }
                );

                continue;
            }

            var friendMessenger = _grainFactory.GetPlayerMessengerGrain(playerId);

            errorCode = await friendMessenger.CanBeAddedByAsync(_state.PlayerId, ct);

            if (errorCode != FriendListErrorCodeType.None)
            {
                failures.Add(
                    new MessengerAcceptFriendFailure { ErrorCode = errorCode, SenderId = playerId }
                );

                continue;
            }

            var friendSnapshot = await _grainFactory.GetPlayerGrain(playerId).GetSummaryAsync(ct);

            await AddFriendshipAsync(friendSnapshot, ct);
            await friendMessenger.OnFriendAddedAsync(snapshot, ct);
        }

        await FlushUpdatesAsync(ct);

        return failures;
    }

    public async Task DeclineFriendRequestsAsync(
        List<PlayerId> playerIds,
        bool declineAll,
        CancellationToken ct
    )
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        if (declineAll)
        {
            await dbCtx
                .MessengerRequests.Where(x => x.RequestedPlayerEntityId == _state.PlayerId.Value)
                .ExecuteDeleteAsync(ct);

            _state.IncomingRequests.Clear();

            return;
        }

        if (playerIds is { Count: > 0 })
        {
            await dbCtx
                .MessengerRequests.Where(x =>
                    x.RequestedPlayerEntityId == _state.PlayerId.Value
                    && playerIds.Contains(x.PlayerEntityId)
                )
                .ExecuteDeleteAsync(ct);

            foreach (var playerId in playerIds)
                _state.IncomingRequests.Remove(playerId);
        }
    }

    public async Task<MessengerRequestFriendResult> SendFriendRequestAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        // Asking yourself would be this grain calling itself through a reference, which waits
        // for a turn that can only start once this one ends.
        if (playerId == _state.PlayerId)
            return new MessengerRequestFriendResult(false);

        if (_state.Friends.Count >= GetFriendLimit())
            return new MessengerRequestFriendResult(
                false,
                FriendListErrorCodeType.YouHitFriendLimit
            );

        if (_state.BlockedPlayerIds.Contains(playerId))
            return new MessengerRequestFriendResult(false, FriendListErrorCodeType.BlockedByYou);

        if (
            _state.Friends.TryGetValue(playerId, out var friend)
            || _state.IncomingRequests.TryGetValue(playerId, out var request)
        )
            return new MessengerRequestFriendResult(false);

        var snapshot = await _grainFactory.GetPlayerGrain(_state.PlayerId).GetSummaryAsync(ct);
        var result = await _grainFactory
            .GetPlayerMessengerGrain(playerId)
            .ReceieveFriendRequestAsync(snapshot, ct);

        if (!result.Success)
            return result;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        dbCtx.Add(
            new MessengerRequestEntity
            {
                PlayerEntityId = _state.PlayerId.Value,
                RequestedPlayerEntityId = playerId.Value,
                PlayerEntity = null!,
                RequestedPlayerEntity = null!,
            }
        );

        await dbCtx.SaveChangesAsync(ct);

        return new MessengerRequestFriendResult(true);
    }

    public async Task<MessengerRequestFriendResult> ReceieveFriendRequestAsync(
        PlayerSummarySnapshot snapshot,
        CancellationToken ct
    )
    {
        if (_state.Friends.Count >= GetFriendLimit())
            return new MessengerRequestFriendResult(
                false,
                FriendListErrorCodeType.TheyHitFriendLimit
            );

        if (_state.BlockedPlayerIds.Contains(snapshot.PlayerId))
            return new MessengerRequestFriendResult(false, FriendListErrorCodeType.BlockedByThem);

        if (
            _state.Friends.TryGetValue(snapshot.PlayerId, out var friend)
            || _state.IncomingRequests.TryGetValue(snapshot.PlayerId, out var request)
        )
            return new MessengerRequestFriendResult(false);

        var requestDto = new MessengerRequestDto
        {
            RequestId = snapshot.PlayerId,
            RequesterPlayerId = snapshot.PlayerId,
            RequesterName = snapshot.Name,
            RequesterFigure = snapshot.Figure,
        };

        _state.IncomingRequests.Add(requestDto.RequesterPlayerId, requestDto);

        await _grainFactory
            .GetPlayerPresenceGrain(_state.PlayerId)
            .OnReceiveFriendRequestAsync(requestDto, ct);

        return new MessengerRequestFriendResult(true, FriendListErrorCodeType.None);
    }

    public async Task BlockPlayerAsync(PlayerId targetId, CancellationToken ct)
    {
        if (_state.BlockedPlayerIds.Contains(targetId))
            return;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        dbCtx.MessengerBlocked.Add(
            new MessengerBlockedEntity
            {
                PlayerEntityId = _state.PlayerId.Value,
                BlockedPlayerEntityId = targetId.Value,
                PlayerEntity = null!,
                BlockedPlayerEntity = null!,
            }
        );

        await dbCtx.SaveChangesAsync(ct);

        _state.BlockedPlayerIds.Add(targetId);

        await RemoveFriendsAsync([targetId], ct);

        await _grainFactory
            .GetPlayerPresenceGrain(_state.PlayerId)
            .OnBlockPlayerUpdatedAsync(targetId, 1, ct);
    }

    public async Task UnblockPlayerAsync(PlayerId targetId, CancellationToken ct)
    {
        if (!_state.BlockedPlayerIds.Contains(targetId))
            return;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        await dbCtx
            .MessengerBlocked.Where(x =>
                x.PlayerEntityId == _state.PlayerId.Value
                && x.BlockedPlayerEntityId == targetId.Value
            )
            .ExecuteDeleteAsync(ct);

        _state.BlockedPlayerIds.Remove(targetId);

        await _grainFactory
            .GetPlayerPresenceGrain(_state.PlayerId)
            .OnBlockPlayerUpdatedAsync(targetId, 0, ct);
    }

    public async Task<MessengerIgnoreResultType> IgnorePlayerAsync(
        PlayerId targetId,
        CancellationToken ct
    )
    {
        MessengerIgnoreResultType result = MessengerIgnoreResultType.AlreadyIgnored;

        if (!_state.IgnoredPlayerIds.Contains(targetId))
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            if (_state.IgnoredPlayerIds.Count >= _playerConfig.MessengerMaxIgnore)
            {
                var oldest = await dbCtx
                    .MessengerIgnored.Where(i => i.PlayerEntityId == _state.PlayerId.Value)
                    .OrderBy(i => i.Id)
                    .FirstOrDefaultAsync(ct);

                if (oldest is not null)
                {
                    dbCtx.MessengerIgnored.Remove(oldest);
                    _state.IgnoredPlayerIds.Remove(oldest.IgnoredPlayerEntityId);
                }

                result = MessengerIgnoreResultType.OldestRemoved;
            }
            else
            {
                result = MessengerIgnoreResultType.Success;
            }

            dbCtx.MessengerIgnored.Add(
                new MessengerIgnoredEntity
                {
                    PlayerEntityId = _state.PlayerId.Value,
                    IgnoredPlayerEntityId = targetId.Value,
                    PlayerEntity = null!,
                    IgnoredPlayerEntity = null!,
                }
            );

            await dbCtx.SaveChangesAsync(ct);

            _state.IgnoredPlayerIds.Add(targetId);

            await _grainFactory
                .GetPlayerPresenceGrain(_state.PlayerId)
                .OnIgnoredUpdatedAsync([.. _state.IgnoredPlayerIds], ct);
        }

        return result;
    }

    public async Task<MessengerIgnoreResultType> UnignorePlayerAsync(
        PlayerId targetId,
        CancellationToken ct
    )
    {
        if (_state.IgnoredPlayerIds.Contains(targetId))
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            await dbCtx
                .MessengerIgnored.Where(x =>
                    x.PlayerEntityId == _state.PlayerId.Value
                    && x.IgnoredPlayerEntityId == targetId.Value
                )
                .ExecuteDeleteAsync(ct);

            _state.IgnoredPlayerIds.Remove(targetId);

            await _grainFactory
                .GetPlayerPresenceGrain(_state.PlayerId)
                .OnIgnoredUpdatedAsync([.. _state.IgnoredPlayerIds], ct);
        }

        return MessengerIgnoreResultType.Unignored;
    }

    public Task UpdateFriendsAsync(PlayerSummarySnapshot snapshot, CancellationToken ct) =>
        Task.WhenAll(
            _state.Friends.Values.Select(friend =>
                _grainFactory
                    .GetPlayerMessengerGrain(friend.PlayerId)
                    .RecieveFriendUpdateAsync(snapshot, ct)
            )
        );

    public Task RecieveFriendUpdateAsync(PlayerSummarySnapshot snapshot, CancellationToken ct)
    {
        if (_state.Friends.TryGetValue(snapshot.PlayerId, out var friendDto))
        {
            _state.Friends[snapshot.PlayerId] = MessengerFriendDto.FromSummary(snapshot) with
            {
                CategoryId = friendDto.CategoryId,
                RelationshipStatus = friendDto.RelationshipStatus,
            };

            ForceUpdate(snapshot.PlayerId);
        }

        return Task.CompletedTask;
    }

    public async Task<bool> SetRelationshipStatusAsync(
        PlayerId friendId,
        MessengerFriendRelationType status,
        CancellationToken ct
    )
    {
        if (!_state.Friends.TryGetValue(friendId, out var friendDto))
            return false;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        await dbCtx
            .MessengerFriends.Where(f =>
                f.PlayerEntityId == (int)_state.PlayerId && f.FriendPlayerEntityId == (int)friendId
            )
            .ExecuteUpdateAsync(up => up.SetProperty(f => f.RelationType, status), ct);

        _state.Friends[friendDto.PlayerId] = friendDto with { RelationshipStatus = status };

        ForceUpdate(friendDto.PlayerId);

        await FlushUpdatesAsync(ct);

        return true;
    }

    public async Task<(
        List<MessengerSearchResultSnapshot> Friends,
        List<MessengerSearchResultSnapshot> Others
    )> SearchPlayersAsync(string query, CancellationToken ct)
    {
        var friends = new List<MessengerSearchResultSnapshot>();
        var others = new List<MessengerSearchResultSnapshot>();

        if (string.IsNullOrWhiteSpace(query))
            return (friends, others);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var results = await dbCtx
            .Players.AsNoTracking()
            .Where(p => EF.Functions.Like(p.Name, $"%{query}%"))
            .Take(_playerConfig.MessengerSearchLimit)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Motto,
                x.Figure,
                x.Gender,
            })
            .ToListAsync(ct);

        var onlineResults = await Task.WhenAll(
            results
                .Where(player => player.Id != (int)_state.PlayerId)
                .ToList()
                .Select(async x =>
                {
                    var presence = _grainFactory.GetPlayerPresenceGrain(PlayerId.Parse(x.Id));
                    var isOnline = await presence.HasActiveSessionAsync(ct);

                    return (x, isOnline);
                })
                .ToList()
        );

        foreach (var (player, isOnline) in onlineResults)
        {
            var snapshot = new MessengerSearchResultSnapshot
            {
                PlayerId = PlayerId.Parse(player.Id),
                Name = player.Name,
                Motto = player.Motto ?? string.Empty,
                Online = isOnline,
                FollowingAllowed = false,
                UnknownString = string.Empty,
                Gender = player.Gender,
                Figure = player.Figure,
                RealName = string.Empty,
            };

            if (_state.Friends.ContainsKey(snapshot.PlayerId))
            {
                friends.Add(snapshot with { FollowingAllowed = true });
            }
            else
            {
                others.Add(snapshot);
            }
        }

        return (friends, others);
    }

    public async Task<bool> SendMessageAsync(
        PlayerId recipientId,
        string message,
        int confirmationId,
        string senderName,
        string senderFigure,
        CancellationToken ct
    )
    {
        if (!_state.Friends.TryGetValue(recipientId, out var friend))
            return false;

        var now = DateTime.UtcNow;
        var sessionMsgId = _state.NextSessionMessageId++.ToString();

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var messageEntity = new MessengerMessageEntity
        {
            SenderPlayerEntityId = _state.PlayerId.Value,
            ReceiverPlayerEntityId = recipientId.Value,
            Message = message,
            Timestamp = now,
            SenderPlayerEntity = null!,
            ReceiverPlayerEntity = null!,
        };

        dbCtx.MessengerMessages.Add(messageEntity);

        await dbCtx.SaveChangesAsync(ct);

        AddToSessionHistory(
            recipientId,
            new MessageHistoryEntrySnapshot
            {
                SenderId = _state.PlayerId,
                SenderName = senderName,
                SenderFigure = senderFigure,
                Message = message,
                MessageId = sessionMsgId,
                SentAtUtc = now,
            }
        );

        return await _grainFactory
            .GetPlayerMessengerGrain(friend.PlayerId)
            .ReceiveMessageAsync(
                _state.PlayerId,
                message,
                now,
                sessionMsgId,
                confirmationId,
                _state.PlayerId,
                senderName,
                senderFigure,
                ct,
                messageEntity.Id
            );
    }

    public async Task<bool> ReceiveMessageAsync(
        int chatId,
        string messageText,
        DateTime sentAtUtc,
        string messageId,
        int confirmationId,
        PlayerId senderId,
        string senderName,
        string senderFigure,
        CancellationToken ct,
        int dbMessageId = 0
    )
    {
        if (!_state.Friends.TryGetValue(senderId, out var friend)) // TODO check if im online
            return false;

        var sessionMsgId = _state.NextSessionMessageId++.ToString();

        AddToSessionHistory(
            senderId,
            new MessageHistoryEntrySnapshot
            {
                SenderId = PlayerId.Parse(senderId),
                SenderName = senderName,
                SenderFigure = senderFigure,
                Message = messageText,
                MessageId = sessionMsgId,
                SentAtUtc = sentAtUtc,
            }
        );

        await _grainFactory.SendComposerToPlayerAsync(
            _state.PlayerId,
            new NewConsoleMessageMessageComposer
            {
                ChatId = chatId,
                Message = messageText,
                SecondsSinceSent = (int)(DateTime.UtcNow - sentAtUtc).TotalSeconds,
                MessageId = sessionMsgId,
                ConfirmationId = confirmationId,
                SenderId = senderId,
                SenderName = senderName,
                SenderFigure = senderFigure,
            },
            ct
        );

        // Queue delivered-flag update — flushed periodically by timer to avoid per-message DB writes
        // Bounded here as well as on a failed flush: between two ticks a busy conversation could
        // otherwise grow it without limit. The flag is cosmetic, so a dropped id costs nothing.
        if (
            dbMessageId > 0
            && _state.PendingDeliveredIds.Count < _playerConfig.MessengerMaxPendingDelivered
        )
            _state.PendingDeliveredIds.Add(dbMessageId);

        return true;
    }

    /// <summary>
    /// Marks buffered messages delivered. Failures are logged and the ids are put back so the
    /// next tick retries them; the flag is cosmetic, so a lost batch never blocks the grain.
    /// </summary>
    private async Task FlushDeliveredMessagesAsync(CancellationToken ct)
    {
        if (_state.PendingDeliveredIds.Count == 0)
            return;

        var messageIds = _state.PendingDeliveredIds.ToList();

        _state.PendingDeliveredIds.Clear();

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            await dbCtx
                .MessengerMessages.Where(x => messageIds.Contains(x.Id))
                .ExecuteUpdateAsync(x => x.SetProperty(m => m.Delivered, true), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to flag {MessageCount} messages delivered for player {PlayerId}",
                messageIds.Count,
                _state.PlayerId
            );

            foreach (var messageId in messageIds)
            {
                if (_state.PendingDeliveredIds.Count >= _playerConfig.MessengerMaxPendingDelivered)
                    break;

                _state.PendingDeliveredIds.Add(messageId);
            }
        }
    }

    private async Task FlushUpdatesAsync(CancellationToken ct)
    {
        var updates = await GetPendingUpdatesAsync(ct);

        await _grainFactory
            .GetPlayerPresenceGrain(_state.PlayerId)
            .FlushMessengerUpdatesAsync(_state.Categories, updates, ct);
    }

    public Task<List<MessengerCategoryDto>> GetCategoriesAsync(CancellationToken ct) =>
        Task.FromResult(_state.Categories.ToList());

    public Task<List<MessengerFriendDto>> GetFriendsAsync(CancellationToken ct) =>
        Task.FromResult(_state.Friends.Values.ToList());

    public Task<List<MessengerRequestDto>> GetRequestsAsync(CancellationToken ct) =>
        Task.FromResult(_state.IncomingRequests.Values.ToList());

    public Task<List<PlayerId>> GetIgnoredAsync(CancellationToken ct) =>
        Task.FromResult(_state.IgnoredPlayerIds.ToList());

    public Task<List<MessengerUpdateSnapshot>> GetPendingUpdatesAsync(CancellationToken ct)
    {
        var updates = _state.PendingUpdates.Values.ToList();

        _state.PendingUpdates.Clear();

        return Task.FromResult(updates);
    }

    public Task<List<RelationshipStatusEntrySnapshot>> GetRelationshipStatusInfoAsync(
        CancellationToken ct
    )
    {
        var entries = new List<RelationshipStatusEntrySnapshot>();

        var grouped = _state
            .Friends.Values.Where(f => f.RelationshipStatus > MessengerFriendRelationType.Zero)
            .GroupBy(f => f.RelationshipStatus);

        foreach (var group in grouped)
        {
            var friends = group.ToList();
            var random = friends[Random.Shared.Next(friends.Count)];

            entries.Add(
                new RelationshipStatusEntrySnapshot
                {
                    RelationshipStatusType = group.Key,
                    FriendCount = friends.Count,
                    RandomFriendId = random.PlayerId,
                    RandomFriendName = random.Name,
                    RandomFriendFigure = random.Figure,
                }
            );
        }

        return Task.FromResult(entries);
    }

    private int GetFriendLimit() => _playerConfig.MessengerNormalFriendLimit;

    private void ForceUpdate(PlayerId friendId)
    {
        if (!_state.Friends.TryGetValue(friendId, out var friendDto))
            return;

        _state.PendingUpdates.Remove(friendId);

        _state.PendingUpdates.Add(
            friendDto.PlayerId,
            new MessengerUpdateSnapshot
            {
                ActionType = FriendListUpdateActionType.Updated,
                FriendId = friendDto.PlayerId,
                Friend = friendDto,
            }
        );
    }

    private async Task HydrateFromDatabaseAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var categories = await dbCtx
            .MessengerCategories.AsNoTracking()
            .Where(x => x.PlayerEntityId == _state.PlayerId.Value)
            .Select(x => new MessengerCategoryDto { CategoryId = x.Id, Name = x.Name })
            .ToListAsync(ct);
        var friends = await dbCtx
            .MessengerFriends.AsNoTracking()
            .Where(x => x.PlayerEntityId == _state.PlayerId.Value)
            .Select(x => new MessengerFriendDto
            {
                PlayerId = PlayerId.Parse(x.FriendPlayerEntityId),
                Name = x.FriendPlayerEntity.Name,
                Gender = x.FriendPlayerEntity.Gender,
                Online = false,
                FollowingAllowed = true,
                Figure = x.FriendPlayerEntity.Figure,
                CategoryId = x.MessengerCategoryEntityId ?? -1,
                Motto = x.FriendPlayerEntity.Motto ?? string.Empty,
                LastAccess = x.FriendPlayerEntity.UpdatedAt.ToString(
                    MessengerFriendDto.LAST_ACCESS_FORMAT,
                    CultureInfo.InvariantCulture
                ),
                RealName = string.Empty,
                FacebookId = string.Empty,
                PersistedMessageUser = false,
                VipMember = false,
                PocketHabboUser = false,
                RelationshipStatus = x.RelationType,
            })
            .ToListAsync(ct);
        var incomingRequests = await dbCtx
            .MessengerRequests.AsNoTracking()
            .Where(x => x.RequestedPlayerEntityId == _state.PlayerId.Value)
            .Select(x => new MessengerRequestDto
            {
                RequestId = x.Id,
                RequesterPlayerId = PlayerId.Parse(x.PlayerEntityId),
                RequesterName = x.PlayerEntity.Name,
                RequesterFigure = x.PlayerEntity.Figure,
            })
            .ToListAsync(ct);
        var blockedPlayerIds = await dbCtx
            .MessengerBlocked.AsNoTracking()
            .Where(x => x.PlayerEntityId == _state.PlayerId.Value)
            .Select(x => PlayerId.Parse(x.BlockedPlayerEntityId))
            .ToListAsync(ct);
        var ignoredPlayerIds = await dbCtx
            .MessengerIgnored.AsNoTracking()
            .Where(x => x.PlayerEntityId == _state.PlayerId.Value)
            .Select(x => PlayerId.Parse(x.IgnoredPlayerEntityId))
            .ToListAsync(ct);

        _state.Categories.Clear();
        _state.Friends.Clear();
        _state.IncomingRequests.Clear();

        _state.Categories.AddRange(categories);
        _state.BlockedPlayerIds.AddRange(blockedPlayerIds);
        _state.IgnoredPlayerIds.AddRange(ignoredPlayerIds);

        // Nothing persisted says who is online, so ask. Interleaved on the presence side, so
        // asking cannot wait on a presence that is itself waiting on this activation.
        var online = await Task.WhenAll(
            friends.Select(friend =>
                _grainFactory.GetPlayerPresenceGrain(friend.PlayerId).HasActiveSessionAsync(ct)
            )
        );

        for (var i = 0; i < friends.Count; i++)
            _state.Friends.Add(friends[i].PlayerId, friends[i] with { Online = online[i] });

        foreach (var request in incomingRequests)
            _state.IncomingRequests.Add(request.RequesterPlayerId, request);
    }

    private void AddToSessionHistory(int chatPartnerId, MessageHistoryEntrySnapshot entry)
    {
        if (!_state.Messages.TryGetValue(chatPartnerId, out var history))
        {
            history = [];
            _state.Messages[chatPartnerId] = history;
        }

        history.Add(entry);

        if (history.Count > _playerConfig.MaxSessionMessagesPerConversation)
            history.RemoveAt(0);

        // TODO save this in db periodically
    }
}
