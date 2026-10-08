using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Moderation;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums.Messenger;
using Turbo.Primitives.Players.Grains.Messenger;
using Turbo.Primitives.Players.Messenger;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Messenger;

namespace Turbo.Players.Grains.Messenger;

/// <summary>
/// Owns a player's friend list, requests, ignore list and console conversations. Friend,
/// request and message rows write through to the database as they happen, nothing is buffered,
/// and so the grain has nothing to flush on deactivation. A message row is written delivered
/// when the friend is online to be handed it; one to an offline friend stays undelivered until
/// that friend's messenger starts (<see cref="SendInitAsync"/>), which marks and replays it.
///
/// Two friends act on each other all the time — both message, both log in, both accept — so
/// every call one messenger makes on another is an interleaved tell that touches memory only
/// (<c>On*</c>, <c>Receive*</c>, <c>CanBeAddedBy</c>). The side that starts a change does the
/// database work for both; the other side is only told.
///
/// Most activations are not for the owner: a profile view, a friend request or the LTD raffle
/// weighting wakes this grain to read a count. Activation therefore only loads rows. Whether
/// each friend is online is asked of their presence lazily, by the owner's own reads and by the
/// owner coming online (<see cref="EnsureFriendsOnlineResolvedAsync"/>), and so does the group
/// chat rejoin timer. A friend's changes are pushed only to friends
/// this grain believes online; an offline friend reads them from the database when their
/// messenger next loads, and this grain deactivates when its owner goes offline so it never
/// sits on rows nobody is updating.
/// </summary>
internal sealed class PlayerMessengerGrain : Grain, IPlayerMessengerGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly PlayerConfig _playerConfig;
    private readonly IGrainFactory _grainFactory;
    private readonly IWordFilter _wordFilter;
    private readonly ILogger<IPlayerMessengerGrain> _logger;

    private readonly PlayerMessengerLiveState _state;

    /// <summary>Rejoins the owner's group chats while they are online; see <see cref="RejoinGroupChatsAsync"/>.</summary>
    private IDisposable? _groupChatRejoinTimer;

    public PlayerMessengerGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<PlayerConfig> playerConfig,
        IGrainFactory grainFactory,
        IWordFilter wordFilter,
        ILogger<IPlayerMessengerGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _playerConfig = playerConfig.Value;
        _grainFactory = grainFactory;
        _wordFilter = wordFilter;
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

    public override Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        LeaveAllGroupChats();

        return Task.CompletedTask;
    }

    public async Task<FriendListErrorCodeType> CanBeAddedByAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        // Answered between the accepting side's other awaits, so two accepts in the same instant
        // can each see one free slot. One friend over the limit is the worst case, and the
        // alternative is the two grains waiting on each other. The limit itself is asked of the
        // permission grain, which never calls a messenger, so waiting on it cannot deadlock.
        if (_state.Friends.Count >= await GetFriendLimitAsync(ct))
            return FriendListErrorCodeType.TheyHitFriendLimit;

        if (_state.BlockedPlayerIds.Contains(playerId))
            return FriendListErrorCodeType.BlockedByThem;

        return FriendListErrorCodeType.None;
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

        var requests = await dbCtx
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
            .ToListAsync(ct);

        dbCtx.MessengerRequests.RemoveRange(requests);

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

    private FriendListErrorCodeType CanAddFriend(PlayerId playerId, int friendLimit)
    {
        var errorCode = FriendListErrorCodeType.None;

        if (_state.Friends.Count >= friendLimit)
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
        var friendLimit = await GetFriendLimitAsync(ct);

        // Local checks first, counting the ones already let through against the limit, as the
        // one-at-a-time version did by adding each friend before checking the next.
        foreach (var playerId in playerIds.Distinct().Select(PlayerId.Parse))
        {
            var errorCode =
                _state.Friends.Count + candidates.Count >= friendLimit
                    ? FriendListErrorCodeType.YouHitFriendLimit
                    : CanAddFriend(playerId, friendLimit);

            if (errorCode != FriendListErrorCodeType.None)
            {
                failures.Add(
                    new MessengerAcceptFriendFailure { ErrorCode = errorCode, SenderId = playerId }
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

        if (_state.Friends.Count >= await GetFriendLimitAsync(ct))
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

        // Asked before anything is written. The settings grain never calls a messenger, so
        // waiting on it cannot come back here.
        var targetSettings = await _grainFactory
            .GetPlayerSettingsGrain(playerId)
            .GetSettingsAsync(ct);

        if (targetSettings.FriendRequestsDisabled)
            return new MessengerRequestFriendResult(
                false,
                FriendListErrorCodeType.FriendRequestsDisabled
            );

        var request = new MessengerRequestEntity
        {
            PlayerEntityId = _state.PlayerId.Value,
            RequestedPlayerEntityId = playerId.Value,
            PlayerEntity = null!,
            RequestedPlayerEntity = null!,
        };

        // Written before the recipient is told, because the tell shows the request to their
        // client at once: a failed write must never leave a request that exists only in the
        // other grain's memory. The pair is unique, so a request already standing is answered
        // here instead of failing the insert.
        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            if (
                await dbCtx.MessengerRequests.AnyAsync(
                    x =>
                        x.PlayerEntityId == _state.PlayerId.Value
                        && x.RequestedPlayerEntityId == playerId.Value,
                    ct
                )
            )
                return new MessengerRequestFriendResult(false);

            dbCtx.MessengerRequests.Add(request);

            await dbCtx.SaveChangesAsync(ct);
        }

        var snapshot = await _grainFactory.GetPlayerGrain(_state.PlayerId).GetSummaryAsync(ct);
        var result = await _grainFactory
            .GetPlayerMessengerGrain(playerId)
            .ReceieveFriendRequestAsync(snapshot, ct);

        if (!result.Success)
        {
            // Only the row written above: the recipient refusing says nothing about any other.
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            await dbCtx.MessengerRequests.Where(x => x.Id == request.Id).ExecuteDeleteAsync(ct);

            return result;
        }

        return new MessengerRequestFriendResult(true);
    }

    public async Task<MessengerRequestFriendResult> ReceieveFriendRequestAsync(
        PlayerSummarySnapshot snapshot,
        CancellationToken ct
    )
    {
        // The limit is asked of the permission grain, which never calls a messenger, so this
        // interleaved call waiting on it cannot deadlock.
        if (_state.Friends.Count >= await GetFriendLimitAsync(ct))
            return new MessengerRequestFriendResult(
                false,
                FriendListErrorCodeType.TheyHitFriendLimit
            );

        if (_state.BlockedPlayerIds.Contains(snapshot.PlayerId))
            return new MessengerRequestFriendResult(false, FriendListErrorCodeType.BlockedByThem);

        if (
            _state.Friends.ContainsKey(snapshot.PlayerId)
            || _state.IncomingRequests.ContainsKey(snapshot.PlayerId)
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

        // Told, not awaited: this is an interleaved tell, and tells await nothing.
        _grainFactory
            .SendComposerToPlayerAsync(
                _state.PlayerId,
                new NewFriendRequestMessageComposer { Request = requestDto },
                CancellationToken.None
            )
            .LogAndForget(_logger, "show player {PlayerId} a friend request", _state.PlayerId);

        return new MessengerRequestFriendResult(true, FriendListErrorCodeType.None);
    }

    public async Task BlockPlayerAsync(PlayerId targetId, CancellationToken ct)
    {
        // Blocking yourself would end in this grain calling itself through a reference.
        if (
            targetId <= 0
            || targetId == _state.PlayerId
            || _state.BlockedPlayerIds.Contains(targetId)
        )
            return;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            // A request either way is dropped with the block, in the same save: accepting it
            // later could only fail against the block.
            var requests = await dbCtx
                .MessengerRequests.Where(x =>
                    (
                        x.PlayerEntityId == _state.PlayerId.Value
                        && x.RequestedPlayerEntityId == targetId.Value
                    )
                    || (
                        x.PlayerEntityId == targetId.Value
                        && x.RequestedPlayerEntityId == _state.PlayerId.Value
                    )
                )
                .ToListAsync(ct);

            dbCtx.MessengerRequests.RemoveRange(requests);
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
        }

        _state.BlockedPlayerIds.Add(targetId);
        _state.IncomingRequests.Remove(targetId);

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

    /// <summary>
    /// Adds <paramref name="targetId"/> to the ignore list, dropping the oldest entry at the
    /// limit. The result is the whole answer: the client applies it to its own copy
    /// (<c>IgnoredUsersManager.onIgnoreResult</c>, which on <see
    /// cref="MessengerIgnoreResultType.OldestRemoved"/> drops its first entry), so the full list
    /// is not sent again; doing both removed a second entry from the client's copy.
    /// </summary>
    public async Task<MessengerIgnoreResultType> IgnorePlayerAsync(
        PlayerId targetId,
        CancellationToken ct
    )
    {
        if (
            targetId <= 0
            || targetId == _state.PlayerId
            || _state.IgnoredPlayerIds.Contains(targetId)
        )
            return MessengerIgnoreResultType.AlreadyIgnored;

        var result = MessengerIgnoreResultType.Success;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        MessengerIgnoredEntity? oldest = null;

        if (_state.IgnoredPlayerIds.Count >= _playerConfig.MessengerMaxIgnore)
        {
            oldest = await dbCtx
                .MessengerIgnored.Where(i => i.PlayerEntityId == _state.PlayerId.Value)
                .OrderBy(i => i.Id)
                .FirstOrDefaultAsync(ct);

            if (oldest is not null)
            {
                dbCtx.MessengerIgnored.Remove(oldest);

                result = MessengerIgnoreResultType.OldestRemoved;
            }
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

        // Only once saved, so a failed write leaves the list as the database has it.
        if (oldest is not null)
            _state.IgnoredPlayerIds.Remove(PlayerId.Parse(oldest.IgnoredPlayerEntityId));

        _state.IgnoredPlayerIds.Add(targetId);

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

        LeaveAllGroupChats();

        DeactivateOnIdle();
    }

    public async Task NotifyFriendsAsync(
        FriendNotificationCodeType typeCode,
        string message,
        CancellationToken ct
    )
    {
        await EnsureFriendsOnlineResolvedAsync(ct);

        var onlineFriendIds = _state
            .Friends.Values.Where(friend => friend.Online)
            .Select(friend => friend.PlayerId)
            .ToList();

        if (onlineFriendIds.Count == 0)
            return;

        await _grainFactory.SendComposerToPlayersAsync(
            onlineFriendIds,
            new FriendNotificationMessageComposer
            {
                AvatarId = _state.PlayerId.Value.ToString(),
                TypeCode = typeCode,
                Message = message,
            },
            ct
        );
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

            // Sent now: a friend going online or offline is what the open conversation notes
            // ("Your friend went offline", MainView.setOnlineStatus) and what raises the friend
            // online bubble. Left pending, it waited for the client's FriendListUpdate poll,
            // which HabboFriendList sends once every 1,000,000 ms.
            FlushUpdates();
        }

        return Task.CompletedTask;
    }

    public async Task<bool> SetRelationshipStatusAsync(
        PlayerId friendId,
        MessengerFriendRelationType status,
        CancellationToken ct
    )
    {
        // RelationshipStatusSelector sends 0 to 3; anything else is not a status the list draws.
        if (!Enum.IsDefined(status) || !_state.Friends.TryGetValue(friendId, out var friendDto))
        {
            _logger.LogWarning(
                "Player {PlayerId} set relationship {Status} for {FriendId}, who is not a friend or not a status",
                _state.PlayerId,
                status,
                friendId
            );

            return false;
        }

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
                friends.Add(snapshot with { FollowingAllowed = isOnline });
            }
            else
            {
                others.Add(snapshot);
            }
        }

        return (friends, others);
    }

    public Task<InstantMessageErrorCodeType?> CanReceiveMessageAsync(
        PlayerId senderId,
        CancellationToken ct
    ) => Task.FromResult(GetMessageRefusal(senderId));

    /// <summary>
    /// Why the owner would refuse a console message from <paramref name="senderId"/>. Only a
    /// friendship carries messages; a block ends the friendship as well, and is checked in case
    /// the rows and this grain ever disagree. The ignore list is not asked: it mutes room chat
    /// (<c>IgnoredUsersManager</c>), not the console.
    /// </summary>
    private InstantMessageErrorCodeType? GetMessageRefusal(PlayerId senderId) =>
        !_state.Friends.ContainsKey(senderId) || _state.BlockedPlayerIds.Contains(senderId)
            ? InstantMessageErrorCodeType.NotFriend
            : null;

    public async Task<InstantMessageErrorCodeType?> SendMessageAsync(
        PlayerId recipientId,
        string message,
        int confirmationId,
        string senderName,
        string senderFigure,
        CancellationToken ct
    )
    {
        var text = _wordFilter.FilterAndTruncate(message, _playerConfig.MessengerMaxMessageLength);

        // MainView.onInput sends nothing empty; one that arrives has nothing to store or confirm.
        if (text.Length == 0)
            return null;

        if (!TryTakeMessageSlot())
        {
            _logger.LogWarning(
                "Player {PlayerId} sent console messages faster than the messenger allows",
                _state.PlayerId
            );

            return InstantMessageErrorCodeType.OfflineFailed;
        }

        // A negative chat id is a group chat (MainView.startConversation).
        if (recipientId < 0)
            return await SendGroupChatMessageAsync(
                recipientId,
                text,
                confirmationId,
                senderName,
                senderFigure,
                ct
            );

        // Messaging yourself would be this grain calling itself through a reference.
        if (recipientId == _state.PlayerId || !_state.Friends.ContainsKey(recipientId))
            return InstantMessageErrorCodeType.NotFriend;

        // Asked before the row is written, so the usual refusal leaves nothing to undo. Both
        // calls are interleaved on the other side.
        var recipientMessenger = _grainFactory.GetPlayerMessengerGrain(recipientId);
        var refusal = await recipientMessenger.CanReceiveMessageAsync(_state.PlayerId, ct);

        if (refusal is not null)
            return refusal;

        var recipientPresence = _grainFactory.GetPlayerPresenceGrain(recipientId);
        var online = await recipientPresence.HasActiveSessionAsync(ct);
        var now = DateTime.UtcNow;

        // Delivered is written with the row: an online friend is handed it below, an offline one
        // gets it from their messenger's next start (SendInitAsync), which replays only rows
        // still undelivered. No flag is left to a later write that could be lost, which is what
        // would make the same message show twice.
        var messageEntity = new MessengerMessageEntity
        {
            SenderPlayerEntityId = _state.PlayerId.Value,
            ReceiverPlayerEntityId = recipientId.Value,
            Message = text,
            Timestamp = now,
            Delivered = online,
            SenderPlayerEntity = null!,
            ReceiverPlayerEntity = null!,
        };

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            dbCtx.MessengerMessages.Add(messageEntity);

            await dbCtx.SaveChangesAsync(ct);
        }

        // The friend may have logged in between the check and the write, with their messenger
        // start already past: without a second look the message would wait for the next login.
        // Marked before it is handed over, so a start running now does not replay it too (and if
        // it already read the row, the client drops the second copy by its message id).
        if (!online && await recipientPresence.HasActiveSessionAsync(ct))
            online = await TrySetDeliveredAsync(messageEntity.Id, true, ct);

        if (online)
        {
            try
            {
                refusal = await recipientMessenger.ReceiveMessageAsync(
                    _state.PlayerId,
                    text,
                    now,
                    messageEntity.Id,
                    senderName,
                    senderFigure,
                    ct
                );
            }
            catch (Exception)
            {
                // Not handed over after all: leave it for the next messenger start. The failure
                // itself is the caller's to log.
                await TrySetDeliveredAsync(messageEntity.Id, false, CancellationToken.None);

                throw;
            }

            if (refusal is not null)
            {
                // The friendship ended between the check and the write: the message was never
                // sent, so it must not stay in either side's history.
                await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

                await dbCtx
                    .MessengerMessages.Where(x => x.Id == messageEntity.Id)
                    .ExecuteDeleteAsync(ct);

                return refusal;
            }
        }

        // The sender's copy carries the confirmation id: it turns their pending (grey) bubble
        // into the sent message (MainView.onConfirmOwnChatMessage). Only a stored, accepted
        // message is confirmed; an offline friend's is, since it waits in the row.
        _grainFactory
            .SendComposerToPlayerAsync(
                _state.PlayerId,
                new NewConsoleMessageMessageComposer
                {
                    ChatId = recipientId,
                    Message = text,
                    SecondsSinceSent = 0,
                    MessageId = messageEntity.Id.ToString(CultureInfo.InvariantCulture),
                    ConfirmationId = confirmationId,
                    SenderId = _state.PlayerId,
                    SenderName = senderName,
                    SenderFigure = senderFigure,
                },
                CancellationToken.None
            )
            .LogAndForget(
                _logger,
                "confirm a console message to player {PlayerId}",
                _state.PlayerId
            );

        return null;
    }

    /// <summary>
    /// Sets one message row's delivered flag; true when it was written. A failure is logged and
    /// answered with false, and the caller carries on as though the flag had not changed.
    /// </summary>
    private async Task<bool> TrySetDeliveredAsync(
        int messageId,
        bool delivered,
        CancellationToken ct
    )
    {
        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            await dbCtx
                .MessengerMessages.Where(x => x.Id == messageId)
                .ExecuteUpdateAsync(x => x.SetProperty(m => m.Delivered, delivered), ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to set message {MessageId} delivered to {Delivered} for player {PlayerId}",
                messageId,
                delivered,
                _state.PlayerId
            );

            return false;
        }
    }

    /// <summary>
    /// Takes one send from the owner's message allowance (<c>MessengerMessagesPerWindow</c> per
    /// <c>MessengerMessageWindowMs</c>, direct and group lines together), or false when it is
    /// spent. A group line reaches every listening member, so one player must not be able to
    /// send as fast as the client lets them type.
    /// </summary>
    private bool TryTakeMessageSlot()
    {
        var now = DateTime.UtcNow;
        var windowStart = now - TimeSpan.FromMilliseconds(_playerConfig.MessengerMessageWindowMs);
        var recent = _state.RecentMessageTimesUtc;

        while (recent.Count > 0 && recent.Peek() <= windowStart)
            recent.Dequeue();

        if (recent.Count >= _playerConfig.MessengerMessagesPerWindow)
            return false;

        recent.Enqueue(now);

        return true;
    }

    public async Task OnGuildMembershipsChangedAsync(CancellationToken ct)
    {
        // An offline owner has no group chats listed; the next login lists them fresh.
        if (!_state.FriendsOnlineResolved)
            return;

        var (added, removed) = await ResolveGroupChatsAsync(ct);

        foreach (var chat in added)
            _state.PendingUpdates[chat.PlayerId] = new MessengerUpdateSnapshot
            {
                ActionType = FriendListUpdateActionType.Added,
                FriendId = chat.PlayerId,
                Friend = chat,
            };

        foreach (var chatId in removed)
            _state.PendingUpdates[chatId] = new MessengerUpdateSnapshot
            {
                ActionType = FriendListUpdateActionType.Removed,
                FriendId = chatId,
            };

        FlushUpdates();
    }

    /// <summary>
    /// Brings <see cref="PlayerMessengerLiveState.GroupChats"/> in line with the owner's groups
    /// and joins or leaves each group's chat to match, returning what changed. The player's guild
    /// grain never awaits a messenger and a group's chat calls are interleaved, so neither wait
    /// can come back to this grain.
    /// </summary>
    private async Task<(
        List<MessengerFriendDto> Added,
        List<PlayerId> Removed
    )> ResolveGroupChatsAsync(CancellationToken ct)
    {
        var memberships = _playerConfig.MessengerGroupChatEnabled
            ? await _grainFactory.GetPlayerGuildGrain(_state.PlayerId).GetMembershipsAsync(ct)
            : ImmutableArray<GuildInfoSnapshot>.Empty;

        var current = memberships.ToDictionary(
            guild => PlayerId.Parse(-guild.GroupId.Value),
            guild => new MessengerFriendDto
            {
                PlayerId = PlayerId.Parse(-guild.GroupId.Value),
                Name = guild.GroupName,
                // HabboFriendList.getSmallGroupBadgeBitmap draws a group friend's figure as its badge.
                Figure = guild.BadgeCode,
                Online = true,
                CategoryId = 0,
            }
        );

        var removed = _state.GroupChats.Keys.Where(id => !current.ContainsKey(id)).ToList();
        var joining = current.Values.Where(chat => !_state.GroupChats.ContainsKey(chat.PlayerId));

        foreach (var chatId in removed)
        {
            _state.GroupChats.Remove(chatId);
            LeaveGroupChat(chatId);
        }

        var joined = await Task.WhenAll(
            joining.Select(async chat =>
                (
                    Chat: chat,
                    Joined: await _grainFactory
                        .GetGuildGrain(GuildId.Parse(-chat.PlayerId.Value))
                        .JoinChatAsync(_state.PlayerId, ct)
                )
            )
        );

        var added = new List<MessengerFriendDto>();

        // A group the membership list still names but that no longer has the owner (the two
        // are read at different moments) is left out until the next change.
        foreach (var (chat, isMember) in joined)
        {
            if (!isMember)
                continue;

            _state.GroupChats[chat.PlayerId] = chat;
            added.Add(chat);
        }

        if (_state.GroupChats.Count > 0)
            EnsureGroupChatRejoinTimer();
        else
            StopGroupChatRejoinTimer();

        return (added, removed);
    }

    /// <summary>
    /// A group keeps who listens to its chat in memory only, so a group grain that is loaded
    /// again (a silo restarted, or it was moved) has forgotten this owner while this grain still
    /// lists the chat. Joining again on a timer while the owner is online puts them back within
    /// one period; joining is idempotent and interleaved on the group's side.
    /// </summary>
    private void EnsureGroupChatRejoinTimer()
    {
        if (_groupChatRejoinTimer is not null)
            return;

        _groupChatRejoinTimer = this.RegisterGrainTimer<object?>(
            static async (self, ct) =>
                await ((PlayerMessengerGrain)self!).RejoinGroupChatsAsync(ct),
            this,
            TimeSpan.FromMilliseconds(_playerConfig.MessengerGroupChatRejoinMs),
            TimeSpan.FromMilliseconds(_playerConfig.MessengerGroupChatRejoinMs)
        );
    }

    private void StopGroupChatRejoinTimer()
    {
        _groupChatRejoinTimer?.Dispose();
        _groupChatRejoinTimer = null;
    }

    /// <summary>
    /// Joins every listed group chat again. A group that says the owner is no longer a member is
    /// left to the membership change that is on its way (<see cref="OnGuildMembershipsChangedAsync"/>).
    /// A failed tick is logged and the schedule goes on.
    /// </summary>
    private async Task RejoinGroupChatsAsync(CancellationToken ct)
    {
        try
        {
            await Task.WhenAll(
                _state
                    .GroupChats.Keys.ToList()
                    .Select(chatId =>
                        _grainFactory
                            .GetGuildGrain(GuildId.Parse(-chatId.Value))
                            .JoinChatAsync(_state.PlayerId, ct)
                    )
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to rejoin the group chats of player {PlayerId}",
                _state.PlayerId
            );
        }
    }

    /// <summary>Leaves every group chat: the owner went offline or the grain is going.</summary>
    private void LeaveAllGroupChats()
    {
        StopGroupChatRejoinTimer();

        foreach (var chatId in _state.GroupChats.Keys.ToList())
            LeaveGroupChat(chatId);

        _state.GroupChats.Clear();
    }

    /// <summary>Told, not awaited: leaving only stops sends, and nothing here depends on it.</summary>
    private void LeaveGroupChat(PlayerId chatId) =>
        _grainFactory
            .GetGuildGrain(GuildId.Parse(-chatId.Value))
            .LeaveChatAsync(_state.PlayerId, CancellationToken.None)
            .LogAndForget(
                _logger,
                "leave the chat of group {GuildId} for player {PlayerId}",
                -chatId.Value,
                _state.PlayerId
            );

    /// <summary>
    /// A line to a group chat. It goes to the members listening now and is not stored, so the
    /// sender's copy is confirmed once the group has taken it.
    /// </summary>
    private async Task<InstantMessageErrorCodeType?> SendGroupChatMessageAsync(
        PlayerId chatId,
        string text,
        int confirmationId,
        string senderName,
        string senderFigure,
        CancellationToken ct
    )
    {
        if (!_state.GroupChats.ContainsKey(chatId))
            return InstantMessageErrorCodeType.NotGroupMember;

        // Group lines have no row, so their id only has to be unique for the client, which
        // drops a message id it has already shown.
        var messageId = Guid.NewGuid().ToString("N");
        var sent = await _grainFactory
            .GetGuildGrain(GuildId.Parse(-chatId.Value))
            .SendChatMessageAsync(_state.PlayerId, senderName, senderFigure, text, messageId, ct);

        if (!sent)
            return InstantMessageErrorCodeType.NotGroupMember;

        await _grainFactory.SendComposerToPlayerAsync(
            _state.PlayerId,
            new NewConsoleMessageMessageComposer
            {
                ChatId = chatId,
                Message = text,
                SecondsSinceSent = 0,
                MessageId = messageId,
                ConfirmationId = confirmationId,
                SenderId = _state.PlayerId,
                SenderName = senderName,
                SenderFigure = senderFigure,
            },
            ct
        );

        return null;
    }

    public Task<InstantMessageErrorCodeType?> ReceiveMessageAsync(
        PlayerId senderId,
        string message,
        DateTime sentAtUtc,
        int messageId,
        string senderName,
        string senderFigure,
        CancellationToken ct
    )
    {
        var refusal = GetMessageRefusal(senderId);

        if (refusal is not null)
            return Task.FromResult(refusal);

        // The recipient's copy carries no confirmation id: MainView.addConsoleMessage reads one
        // above zero as the server confirming a message of its own, and drops it. Told, not
        // awaited: this is an interleaved tell, and tells await nothing.
        _grainFactory
            .SendComposerToPlayerAsync(
                _state.PlayerId,
                new NewConsoleMessageMessageComposer
                {
                    ChatId = senderId,
                    Message = message,
                    SecondsSinceSent = (int)(DateTime.UtcNow - sentAtUtc).TotalSeconds,
                    MessageId = messageId.ToString(CultureInfo.InvariantCulture),
                    ConfirmationId = 0,
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

        return Task.FromResult<InstantMessageErrorCodeType?>(null);
    }

    public async Task<List<MessageHistoryEntrySnapshot>> GetMessageHistoryAsync(
        PlayerId chatPartnerId,
        string beforeMessageId,
        CancellationToken ct
    )
    {
        // History off (the default), group chats (negative ids, never stored), and anyone who is
        // not a friend all answer nothing, without a query.
        if (
            _playerConfig.MessengerHistoryPageSize <= 0
            || chatPartnerId <= 0
            || !_state.Friends.ContainsKey(chatPartnerId)
        )
            return [];

        var ownerId = _state.PlayerId.Value;
        var partnerId = chatPartnerId.Value;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var query = dbCtx
            .MessengerMessages.AsNoTracking()
            .Where(x =>
                (x.SenderPlayerEntityId == ownerId && x.ReceiverPlayerEntityId == partnerId)
                || (x.SenderPlayerEntityId == partnerId && x.ReceiverPlayerEntityId == ownerId)
            );

        // MainView.requestHistory sends the oldest message id it holds, or nothing for the
        // newest page. A cursor that is not a message of this conversation pages nothing:
        // answering with the newest page would put those messages before the ones shown.
        if (!string.IsNullOrEmpty(beforeMessageId))
        {
            if (
                !int.TryParse(
                    beforeMessageId,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var cursorId
                )
            )
                return [];

            var cursor = await query
                .Where(x => x.Id == cursorId)
                .Select(x => new { x.Id, x.Timestamp })
                .FirstOrDefaultAsync(ct);

            if (cursor is null)
                return [];

            // Rows written in the same instant are told apart by id, so a page boundary never
            // drops or repeats one.
            query = query.Where(x =>
                x.Timestamp < cursor.Timestamp
                || (x.Timestamp == cursor.Timestamp && x.Id < cursor.Id)
            );
        }

        var rows = await query
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.Id)
            .Take(_playerConfig.MessengerHistoryPageSize)
            .Select(x => new MessageHistoryEntrySnapshot
            {
                SenderId = PlayerId.Parse(x.SenderPlayerEntityId),
                SenderName = x.SenderPlayerEntity.Name,
                SenderFigure = x.SenderPlayerEntity.Figure,
                Message = x.Message,
                MessageId = x.Id.ToString(CultureInfo.InvariantCulture),
                SentAtUtc = x.Timestamp,
            })
            .ToListAsync(ct);

        // MainView.loadMessageHistory puts the page in front of what it shows, in this order.
        rows.Reverse();

        return rows;
    }

    public async Task<List<PlayerId>> SendRoomInviteAsync(
        List<PlayerId> recipientIds,
        string message,
        CancellationToken ct
    )
    {
        var failed = new List<PlayerId>();
        var text = _wordFilter.FilterAndTruncate(
            message,
            _playerConfig.MessengerRoomInviteMaxLength
        );

        // RoomInviteView.sendMsg refuses an empty text with its own alert.
        if (text.Length == 0)
            return failed;

        var now = DateTime.UtcNow;

        if (
            now - _state.LastRoomInviteAtUtc
            < TimeSpan.FromMilliseconds(_playerConfig.MessengerRoomInviteMinIntervalMs)
        )
        {
            _logger.LogWarning(
                "Player {PlayerId} sent a room invitation inside the minimum interval",
                _state.PlayerId
            );

            return [.. recipientIds.Distinct()];
        }

        _state.LastRoomInviteAtUtc = now;

        var friendIds = new List<PlayerId>();

        foreach (var recipientId in recipientIds.Distinct())
        {
            // Yourself would be this grain calling itself through a reference.
            if (
                recipientId == _state.PlayerId
                || !_state.Friends.ContainsKey(recipientId)
                || friendIds.Count >= _playerConfig.MessengerRoomInviteMaxRecipients
            )
            {
                failed.Add(recipientId);

                continue;
            }

            friendIds.Add(recipientId);
        }

        // Each friend is a grain of their own, so they are asked side by side. The presence
        // read and the invitation tell are interleaved on the other side, and the settings
        // grain never calls a messenger, so none of these can wait on this grain.
        var reached = await Task.WhenAll(
            friendIds.Select(async friendId =>
            {
                if (!await _grainFactory.GetPlayerPresenceGrain(friendId).HasActiveSessionAsync(ct))
                    return (FriendId: friendId, Reached: false);

                var settings = await _grainFactory
                    .GetPlayerSettingsGrain(friendId)
                    .GetSettingsAsync(ct);

                // Ignoring invitations is the friend's own choice, so the sender is not told.
                if (!settings.RoomInvitesIgnored)
                    await _grainFactory
                        .GetPlayerMessengerGrain(friendId)
                        .ReceiveRoomInviteAsync(_state.PlayerId, text, ct);

                return (FriendId: friendId, Reached: true);
            })
        );

        failed.AddRange(reached.Where(x => !x.Reached).Select(x => x.FriendId));

        return failed;
    }

    public Task ReceiveRoomInviteAsync(PlayerId senderId, string message, CancellationToken ct)
    {
        if (GetMessageRefusal(senderId) is not null)
            return Task.CompletedTask;

        // Told, not awaited: this is an interleaved tell, and tells await nothing.
        _grainFactory
            .SendComposerToPlayerAsync(
                _state.PlayerId,
                new RoomInviteMessageComposer { SenderId = senderId, Message = message },
                CancellationToken.None
            )
            .LogAndForget(_logger, "show player {PlayerId} a room invitation", _state.PlayerId);

        return Task.CompletedTask;
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

        // Group chats go in the list as friends with the group's negative id; the navigator's
        // friend queries read GetFriendsAsync and never see them.
        List<MessengerFriendDto> friends =
        [
            .. await GetFriendsAsync(ct),
            .. _state.GroupChats.Values,
        ];

        List<IComposer> composers =
        [
            new MessengerInitMessageComposer
            {
                // The limit this player has; the other two are the hotel's tiers, which the
                // client shows beside it.
                UserFriendLimit = await GetFriendLimitAsync(ct),
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

        // After the friend list, so every chat the replay opens is a friend the client knows
        // (MainView.startConversation starts nothing for anyone else).
        var (offlineMessages, replayedUpToId) = await LoadOfflineMessagesAsync(ct);

        // Marked delivered before they go out: if the mark fails they are held back and replayed
        // at the next start instead, rather than sent now and again then.
        if (replayedUpToId > 0 && await TryMarkOfflineMessagesDeliveredAsync(replayedUpToId, ct))
            composers.AddRange(offlineMessages);

        await _grainFactory
            .GetPlayerPresenceGrain(_state.PlayerId)
            .SendComposerAsync(composers, ct);
    }

    /// <summary>
    /// The newest undelivered messages from current friends, oldest first, as the client shows a
    /// live one, and the newest row id among them. Messages from a former friend are left
    /// undelivered: there is no conversation to put them in.
    /// </summary>
    private async Task<(List<IComposer> Composers, int ReplayedUpToId)> LoadOfflineMessagesAsync(
        CancellationToken ct
    )
    {
        var friendIds = _state.Friends.Keys.Select(x => x.Value).ToList();

        if (friendIds.Count == 0)
            return ([], 0);

        var ownerId = _state.PlayerId.Value;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var rows = await dbCtx
            .MessengerMessages.AsNoTracking()
            .Where(x =>
                x.ReceiverPlayerEntityId == ownerId
                && !x.Delivered
                && friendIds.Contains(x.SenderPlayerEntityId)
            )
            .OrderByDescending(x => x.Timestamp)
            .ThenByDescending(x => x.Id)
            .Take(_playerConfig.MessengerOfflineReplayLimit)
            .Select(x => new
            {
                x.Id,
                x.SenderPlayerEntityId,
                x.Message,
                x.Timestamp,
                SenderName = x.SenderPlayerEntity.Name,
                SenderFigure = x.SenderPlayerEntity.Figure,
            })
            .ToListAsync(ct);

        if (rows.Count == 0)
            return ([], 0);

        rows.Reverse();

        var now = DateTime.UtcNow;
        var composers = rows.Select(
                IComposer (x) =>
                    new NewConsoleMessageMessageComposer
                    {
                        ChatId = x.SenderPlayerEntityId,
                        Message = x.Message,
                        SecondsSinceSent = (int)(now - x.Timestamp).TotalSeconds,
                        MessageId = x.Id.ToString(CultureInfo.InvariantCulture),
                        ConfirmationId = 0,
                        SenderId = PlayerId.Parse(x.SenderPlayerEntityId),
                        SenderName = x.SenderName,
                        SenderFigure = x.SenderFigure,
                    }
            )
            .ToList();

        return (composers, rows.Max(x => x.Id));
    }

    /// <summary>
    /// Marks everything from current friends up to the newest replayed row delivered, in one
    /// statement, so rows past the replay limit are not replayed on every login; they remain in
    /// the conversation history. False, logged, when the write failed.
    /// </summary>
    private async Task<bool> TryMarkOfflineMessagesDeliveredAsync(int upToId, CancellationToken ct)
    {
        var ownerId = _state.PlayerId.Value;
        var friendIds = _state.Friends.Keys.Select(x => x.Value).ToList();

        try
        {
            await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

            await dbCtx
                .MessengerMessages.Where(x =>
                    x.ReceiverPlayerEntityId == ownerId
                    && !x.Delivered
                    && x.Id <= upToId
                    && friendIds.Contains(x.SenderPlayerEntityId)
                )
                .ExecuteUpdateAsync(x => x.SetProperty(m => m.Delivered, true), ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to mark offline messages up to {MessageId} delivered for player {PlayerId}",
                upToId,
                _state.PlayerId
            );

            return false;
        }
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

    public Task<bool> IsFriendAsync(PlayerId playerId, CancellationToken ct) =>
        Task.FromResult(_state.Friends.ContainsKey(playerId));

    public Task<bool> IsBlockingAsync(PlayerId playerId, CancellationToken ct) =>
        Task.FromResult(_state.BlockedPlayerIds.Contains(playerId));

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

    /// <summary>
    /// The player's own friend limit: the hotel's normal limit, or what their permissions raise it
    /// to through <c>limit.friends</c>.
    /// </summary>
    private Task<int> GetFriendLimitAsync(CancellationToken ct) =>
        _grainFactory.GetLimitAsync(
            _state.PlayerId,
            PermissionMetaKeys.Limit.FRIENDS,
            _playerConfig.MessengerNormalFriendLimit,
            ct
        );

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
            .OrderBy(x => x.Id)
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
                FollowingAllowed = false,
                Figure = x.FriendPlayerEntity.Figure,
                // Category 0 is the list's own "friends"; -1 is the offline caption, which
                // FriendCategories.addFriend uses for every offline friend itself.
                CategoryId = x.MessengerCategoryEntityId ?? 0,
                Motto = x.FriendPlayerEntity.Motto ?? string.Empty,
                LastAccess = x.FriendPlayerEntity.UpdatedAt.ToString(
                    MessengerFriendDto.LAST_ACCESS_FORMAT,
                    CultureInfo.InvariantCulture
                ),
                RealName = string.Empty,
                FacebookId = string.Empty,
                // Messages to an offline friend are kept and delivered at their next login, so
                // FriendsView.refreshFriendEntry offers the chat button for them too.
                PersistedMessageUser = true,
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
            fresh[rows[i].PlayerId] = rows[i] with
            {
                Online = online[i],
                FollowingAllowed = online[i],
            };

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

        // The owner is online from here on, so their group chats are listed and listened to.
        await ResolveGroupChatsAsync(ct);

        _state.FriendsOnlineResolved = true;
    }
}
