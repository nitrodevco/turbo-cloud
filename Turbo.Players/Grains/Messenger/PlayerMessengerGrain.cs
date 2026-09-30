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
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Networking;
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
///
/// Most activations are not for the owner: a profile view, a friend request or the LTD raffle
/// weighting wakes this grain to read a count. Activation therefore only loads rows. Whether
/// each friend is online is asked of their presence lazily, by the owner's own reads and by the
/// owner coming online (<see cref="EnsureFriendsOnlineResolvedAsync"/>), and the delivered-flag
/// timer starts with the first delivered message. A friend's changes are pushed only to friends
/// this grain believes online; an offline friend reads them from the database when their
/// messenger next loads, and this grain deactivates when its owner goes offline so it never
/// sits on rows nobody is updating.
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

        FlushUpdates();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes each friendship from both sides, and drops any request either side had open, with
    /// one delete and one save for the whole batch: two saves on two grains could leave one of
    /// them friends with somebody who is not friends with them.
    /// </summary>
    private async Task AddFriendshipsAsync(
        List<PlayerSummarySnapshot> friends,
        CancellationToken ct
    )
    {
        var friendIds = friends.Select(x => x.PlayerId.Value).ToList();

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        await dbCtx
            .MessengerRequests.Where(x =>
                (
                    friendIds.Contains(x.PlayerEntityId)
                    && x.RequestedPlayerEntityId == _state.PlayerId.Value
                )
                || (
                    x.PlayerEntityId == _state.PlayerId.Value
                    && friendIds.Contains(x.RequestedPlayerEntityId)
                )
            )
            .ExecuteDeleteAsync(ct);

        foreach (var friend in friends)
        {
            dbCtx.Add(NewFriendRow(_state.PlayerId, friend.PlayerId));
            dbCtx.Add(NewFriendRow(friend.PlayerId, _state.PlayerId));
        }

        await dbCtx.SaveChangesAsync(ct);

        foreach (var friend in friends)
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

        FlushUpdates();
    }

    public Task OnFriendRemovedAsync(PlayerId playerId, CancellationToken ct)
    {
        RemoveFriendFromState(playerId);

        FlushUpdates();

        return Task.CompletedTask;
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
        var candidates = new List<PlayerId>();

        // Local checks first, counting the ones already let through against the limit, as the
        // one-at-a-time version did by adding each friend before checking the next.
        foreach (var playerId in playerIds.Distinct().Select(PlayerId.Parse))
        {
            var errorCode =
                _state.Friends.Count + candidates.Count >= GetFriendLimit()
                    ? FriendListErrorCodeType.YouHitFriendLimit
                    : CanAddFriend(playerId);

            if (errorCode != FriendListErrorCodeType.None)
            {
                failures.Add(
                    new MessengerAcceptFriendFailure { ErrorCode = errorCode, SenderId = -1 }
                );

                continue;
            }

            candidates.Add(playerId);
        }

        if (candidates.Count == 0)
            return failures;

        // Each requester is a grain of their own, so their side is asked side by side. Both
        // calls are interleaved on the other side (or on a player grain that never awaits a
        // messenger), so two players accepting each other at once cannot wait on each other.
        var ownSummaryTask = _grainFactory.GetPlayerGrain(_state.PlayerId).GetSummaryAsync(ct);
        var checks = await Task.WhenAll(
            candidates.Select(async playerId =>
            {
                var errorCode = await _grainFactory
                    .GetPlayerMessengerGrain(playerId)
                    .CanBeAddedByAsync(_state.PlayerId, ct);

                var summary =
                    errorCode == FriendListErrorCodeType.None
                        ? await _grainFactory.GetPlayerGrain(playerId).GetSummaryAsync(ct)
                        : null;

                return (PlayerId: playerId, ErrorCode: errorCode, Summary: summary);
            })
        );
        var snapshot = await ownSummaryTask;
        var accepted = new List<PlayerSummarySnapshot>();

        foreach (var (playerId, errorCode, summary) in checks)
        {
            if (summary is null)
            {
                failures.Add(
                    new MessengerAcceptFriendFailure { ErrorCode = errorCode, SenderId = playerId }
                );

                continue;
            }

            accepted.Add(summary);
        }

        if (accepted.Count > 0)
        {
            await AddFriendshipsAsync(accepted, ct);

            await Task.WhenAll(
                accepted.Select(friend =>
                    _grainFactory
                        .GetPlayerMessengerGrain(friend.PlayerId)
                        .OnFriendAddedAsync(snapshot, ct)
                )
            );
        }

        FlushUpdates();

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

        if (_state.Friends.ContainsKey(playerId))
            return new MessengerRequestFriendResult(false);

        // They asked first: asking them back is agreeing, so their request is accepted rather
        // than a second one left waiting beside it.
        if (_state.IncomingRequests.ContainsKey(playerId))
        {
            var failures = await AcceptFriendRequestsAsync([playerId.Value], ct);

            return failures.Count == 0
                ? new MessengerRequestFriendResult(true)
                : new MessengerRequestFriendResult(false, failures[0].ErrorCode);
        }

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

    public Task<MessengerRequestFriendResult> ReceieveFriendRequestAsync(
        PlayerSummarySnapshot snapshot,
        CancellationToken ct
    )
    {
        if (_state.Friends.Count >= GetFriendLimit())
            return Task.FromResult(
                new MessengerRequestFriendResult(false, FriendListErrorCodeType.TheyHitFriendLimit)
            );

        if (_state.BlockedPlayerIds.Contains(snapshot.PlayerId))
            return Task.FromResult(
                new MessengerRequestFriendResult(false, FriendListErrorCodeType.BlockedByThem)
            );

        if (
            _state.Friends.ContainsKey(snapshot.PlayerId)
            || _state.IncomingRequests.ContainsKey(snapshot.PlayerId)
        )
            return Task.FromResult(new MessengerRequestFriendResult(false));

        var requestDto = new MessengerRequestDto
        {
            RequestId = snapshot.PlayerId,
            RequesterPlayerId = snapshot.PlayerId,
            RequesterName = snapshot.Name,
            RequesterFigure = snapshot.Figure,
        };

        _state.IncomingRequests.Add(requestDto.RequesterPlayerId, requestDto);

        // Told, not awaited: this is an interleaved tell, and tells await nothing.
        _grainFactory
            .SendComposerToPlayerAsync(
                _state.PlayerId,
                new NewFriendRequestMessageComposer { Request = requestDto },
                CancellationToken.None
            )
            .LogAndForget(_logger, "show player {PlayerId} a friend request", _state.PlayerId);

        return Task.FromResult(
            new MessengerRequestFriendResult(true, FriendListErrorCodeType.None)
        );
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

        await _grainFactory.SendComposerToPlayerAsync(
            _state.PlayerId,
            new BlockUserUpdateMessageComposer
            {
                Result = MessengerBlockResultType.Blocked,
                UserId = targetId,
            },
            ct
        );
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

        await _grainFactory.SendComposerToPlayerAsync(
            _state.PlayerId,
            new BlockUserUpdateMessageComposer
            {
                Result = MessengerBlockResultType.Unblocked,
                UserId = targetId,
            },
            ct
        );
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

            await _grainFactory.SendComposerToPlayerAsync(
                _state.PlayerId,
                new IgnoredUsersMessageComposer { IgnoredUserIds = [.. _state.IgnoredPlayerIds] },
                ct
            );
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

            await _grainFactory.SendComposerToPlayerAsync(
                _state.PlayerId,
                new IgnoredUsersMessageComposer { IgnoredUserIds = [.. _state.IgnoredPlayerIds] },
                ct
            );
        }

        return MessengerIgnoreResultType.Unignored;
    }

    /// <summary>
    /// Pushes the owner's changed summary to the friends who are online. An offline friend's
    /// messenger is not woken for it: it reads the owner's row when it next loads, and waking
    /// every friend's messenger on each login was a query storm (each one loaded five tables
    /// and asked all of its own friends' presences).
    /// </summary>
    public async Task UpdateFriendsAsync(PlayerSummarySnapshot snapshot, CancellationToken ct)
    {
        // Coming online: find out which friends are, both to notify them now and so the friend
        // list the client asks for next shows them online.
        if (snapshot.IsOnline)
            await EnsureFriendsOnlineResolvedAsync(ct);

        var onlineFriendIds = _state
            .Friends.Values.Where(friend => friend.Online)
            .Select(friend => friend.PlayerId)
            .ToList();

        await Task.WhenAll(
            onlineFriendIds.Select(friendId =>
                _grainFactory
                    .GetPlayerMessengerGrain(friendId)
                    .RecieveFriendUpdateAsync(snapshot, ct)
            )
        );

        if (snapshot.IsOnline)
            return;

        // Gone offline: no friend pushes to this grain any more, so the rows it holds stop
        // being kept. Letting it go means the next activation reads them fresh.
        _state.FriendsOnlineResolved = false;

        DeactivateOnIdle();
    }

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

        FlushUpdates();

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

        // Every keystroke-driven search is a query, so one player gets one per interval.
        var now = DateTime.UtcNow;

        if (
            now - _state.LastSearchAtUtc
            < TimeSpan.FromMilliseconds(_playerConfig.MessengerSearchMinIntervalMs)
        )
            return (friends, others);

        _state.LastSearchAtUtc = now;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        // A prefix match, so the unique index on the name answers it; a leading wildcard read
        // every player row on each search. The client's own wildcards are escaped.
        var pattern = EscapeLikePattern(query) + "%";
        var results = await dbCtx
            .Players.AsNoTracking()
            .Where(p => EF.Functions.Like(p.Name, pattern, LIKE_ESCAPE))
            .OrderBy(p => p.Name)
            .ThenBy(p => p.Id)
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

    public Task<bool> ReceiveMessageAsync(
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
        if (!_state.Friends.ContainsKey(senderId)) // TODO check if im online
            return Task.FromResult(false);

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

        // Told, not awaited: this is an interleaved tell, and tells await nothing.
        _grainFactory
            .SendComposerToPlayerAsync(
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
                CancellationToken.None
            )
            .LogAndForget(
                _logger,
                "deliver a console message to player {PlayerId}",
                _state.PlayerId
            );

        // Queue delivered-flag update — flushed periodically by timer to avoid per-message DB writes
        // Bounded here as well as on a failed flush: between two ticks a busy conversation could
        // otherwise grow it without limit. The flag is cosmetic, so a dropped id costs nothing.
        if (
            dbMessageId > 0
            && _state.PendingDeliveredIds.Count < _playerConfig.MessengerMaxPendingDelivered
        )
        {
            _state.PendingDeliveredIds.Add(dbMessageId);

            EnsureDeliveredFlushTimer();
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Starts the delivered-flag flush timer the first time there is something to flush, so an
    /// activation for a profile view or a friend count registers no timer.
    /// </summary>
    private void EnsureDeliveredFlushTimer()
    {
        if (_deliveredFlushTimer is not null)
            return;

        _deliveredFlushTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) =>
                await ((PlayerMessengerGrain)self!).FlushDeliveredMessagesAsync(ct),
            this,
            TimeSpan.FromMilliseconds(_playerConfig.MessengerDeliveredFlushMs),
            TimeSpan.FromMilliseconds(_playerConfig.MessengerDeliveredFlushMs)
        );
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

    /// <summary>
    /// Sends the pending friend list changes to the owner. Told, not awaited: the interleaved
    /// tells (<see cref="OnFriendAddedAsync"/>, <see cref="OnFriendRemovedAsync"/>) end here and
    /// must await nothing, and one way to flush is all this grain needs.
    /// </summary>
    private void FlushUpdates()
    {
        var updates = _state.PendingUpdates.Values.ToList();

        _state.PendingUpdates.Clear();

        if (updates.Count == 0)
            return;

        _grainFactory
            .SendComposerToPlayerAsync(
                _state.PlayerId,
                new FriendListUpdateMessageComposer
                {
                    Categories = [.. _state.Categories],
                    Updates = updates,
                },
                CancellationToken.None
            )
            .LogAndForget(
                _logger,
                "send friend list updates to player {PlayerId}",
                _state.PlayerId
            );
    }

    public async Task SendInitAsync(CancellationToken ct)
    {
        var categories = await GetCategoriesAsync(ct);
        var friends = await GetFriendsAsync(ct);

        List<IComposer> composers =
        [
            new MessengerInitMessageComposer
            {
                UserFriendLimit = _playerConfig.MessengerUserFriendLimit,
                NormalFriendLimit = _playerConfig.MessengerNormalFriendLimit,
                ExtendedFriendLimit = _playerConfig.MessengerExtendedFriendLimit,
                FriendCategories = categories,
            },
            .. ComposerFragments.Build(
                friends,
                _playerConfig.FriendListFragmentSize,
                (total, index, fragment) =>
                    new FriendListFragmentMessageComposer
                    {
                        TotalFragments = total,
                        FragmentIndex = index,
                        Fragment = [.. fragment],
                    }
            ),
        ];

        await _grainFactory
            .GetPlayerPresenceGrain(_state.PlayerId)
            .SendComposerAsync(composers, ct);
    }

    public async Task<List<MessengerCategoryDto>> GetCategoriesAsync(CancellationToken ct)
    {
        // The owner's own messenger init starts here, so this is where the friend list becomes
        // theirs to see.
        await EnsureFriendsOnlineResolvedAsync(ct);

        return _state.Categories.ToList();
    }

    public async Task<List<MessengerFriendDto>> GetFriendsAsync(CancellationToken ct)
    {
        await EnsureFriendsOnlineResolvedAsync(ct);

        return _state.Friends.Values.ToList();
    }

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

    public Task<int> GetFriendCountAsync(CancellationToken ct) =>
        Task.FromResult(_state.Friends.Count);

    public Task<MessengerProfileRelationSnapshot> GetProfileRelationAsync(
        PlayerId viewerId,
        CancellationToken ct
    ) =>
        Task.FromResult(
            new MessengerProfileRelationSnapshot
            {
                FriendCount = _state.Friends.Count,
                IsFriend = _state.Friends.ContainsKey(viewerId),
                IsFriendRequestSent = _state.IncomingRequests.ContainsKey(viewerId),
            }
        );

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

    /// <summary>The escape character for <see cref="EscapeLikePattern"/>; MySQL's own default.</summary>
    private const string LIKE_ESCAPE = "\\";

    /// <summary>Makes client text match itself literally inside a <c>LIKE</c> pattern.</summary>
    private static string EscapeLikePattern(string text) =>
        text.Replace(LIKE_ESCAPE, LIKE_ESCAPE + LIKE_ESCAPE, StringComparison.Ordinal)
            .Replace("%", LIKE_ESCAPE + "%", StringComparison.Ordinal)
            .Replace("_", LIKE_ESCAPE + "_", StringComparison.Ordinal);

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
        var friends = await LoadFriendRowsAsync(dbCtx, ct);
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

        // Every friend reads offline until the owner's first read asks their presences
        // (EnsureFriendsOnlineResolvedAsync); most activations are not the owner's.
        foreach (var friend in friends)
            _state.Friends.Add(friend.PlayerId, friend);

        _state.FriendsLoadedAtUtc = DateTime.UtcNow;
        _state.FriendsOnlineResolved = false;

        foreach (var request in incomingRequests)
            _state.IncomingRequests.Add(request.RequesterPlayerId, request);
    }

    private Task<List<MessengerFriendDto>> LoadFriendRowsAsync(
        TurboDbContext dbCtx,
        CancellationToken ct
    ) =>
        dbCtx
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

    /// <summary>
    /// Asks every friend's presence whether they are online, once per time the owner comes
    /// online, and re-reads the friend rows first when they were loaded too long ago (the grain
    /// was woken by somebody else while its owner was offline, and heard no updates since).
    /// Only the owner's own reads and the owner coming online call this; a profile view, a
    /// friend count or a friend request never does, so they cost no presence activations.
    ///
    /// Runs inside a non-interleaved turn, but the friend tells (<c>OnFriendAdded</c>,
    /// <c>OnFriendRemoved</c>, <c>RecieveFriendUpdate</c>) interleave with its awaits. Whatever
    /// a tell changed meanwhile is newer than what this read, so an entry is only replaced when
    /// it is still the one that was there before the first await.
    /// </summary>
    private async Task EnsureFriendsOnlineResolvedAsync(CancellationToken ct)
    {
        if (_state.FriendsOnlineResolved)
            return;

        var before = new Dictionary<PlayerId, MessengerFriendDto>(_state.Friends);
        var rows = before.Values.ToList();

        if (
            DateTime.UtcNow - _state.FriendsLoadedAtUtc
            > TimeSpan.FromSeconds(_playerConfig.MessengerFriendRowsFreshSeconds)
        )
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            rows = await LoadFriendRowsAsync(dbCtx, ct);

            _state.FriendsLoadedAtUtc = DateTime.UtcNow;
        }

        // Interleaved on the presence side, so asking cannot wait on a presence that is itself
        // waiting on this grain.
        var online = await Task.WhenAll(
            rows.Select(friend =>
                _grainFactory.GetPlayerPresenceGrain(friend.PlayerId).HasActiveSessionAsync(ct)
            )
        );

        var fresh = new Dictionary<PlayerId, MessengerFriendDto>(rows.Count);

        for (var i = 0; i < rows.Count; i++)
            fresh[rows[i].PlayerId] = rows[i] with { Online = online[i] };

        foreach (var friendId in before.Keys.Union(fresh.Keys).ToList())
        {
            before.TryGetValue(friendId, out var previous);
            _state.Friends.TryGetValue(friendId, out var current);

            // A tell added, removed or refreshed this friend while we were asking.
            if (!ReferenceEquals(previous, current))
                continue;

            if (fresh.TryGetValue(friendId, out var friend))
                _state.Friends[friendId] = friend;
            else
                _state.Friends.Remove(friendId);
        }

        _state.FriendsOnlineResolved = true;
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
