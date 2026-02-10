using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Messenger;
using Turbo.Primitives.FriendList.Enums;
using Turbo.Primitives.FriendList.Grains;
using Turbo.Primitives.Messages.Outgoing.FriendList;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Players.Grains;

internal sealed class MessengerGrain(
    IDbContextFactory<TurboDbContext> dbCtxFactory,
    IGrainFactory grainFactory,
    ILogger<IMessengerGrain> logger
) : Grain, IMessengerGrain
{
    private PlayerId _playerId;

    // In-memory state loaded from DB on activation
    private readonly Dictionary<int, MessengerFriendSnapshot> _friends = [];
    private readonly Dictionary<int, FriendRequestSnapshot> _incomingRequests = [];
    private readonly HashSet<int> _blockedUserIds = [];
    private readonly HashSet<int> _ignoredUserIds = [];
    private readonly List<FriendCategorySnapshot> _categories = [];
    private bool _isLoaded;

    // Session-based message history — cleared on deactivation, never persisted
    private const int MaxSessionMessagesPerConversation = 200;
    private readonly Dictionary<int, List<MessageHistoryEntrySnapshot>> _sessionMessages = [];
    private int _nextSessionMessageId = 1;

    // Tracks whether this player has an active session (set by NotifyOnline/OfflineAsync)
    private bool _isOnline;

    // Batched delivered-flag queue — flushed periodically to avoid per-message DB writes
    private readonly HashSet<int> _pendingDeliveredIds = [];

    public override async Task OnActivateAsync(CancellationToken ct)
    {
        _playerId = PlayerId.Parse((int)this.GetPrimaryKeyLong());

        this.RegisterGrainTimer<object?>(
            static async (self, ct) =>
                await ((MessengerGrain)self!).FlushDeliveredMessagesAsync(ct),
            this,
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(10)
        );

        await LoadFromDatabaseAsync(ct);
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken ct)
    {
        await FlushDeliveredMessagesAsync(ct);
    }

    private async Task LoadFromDatabaseAsync(CancellationToken ct)
    {
        if (_isLoaded)
            return;

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);
        var playerId = (int)_playerId;

        // Load friends (without online checks — those are done in parallel below)
        var friendEntities = await dbCtx
            .MessengerFriends.AsNoTracking()
            .Include(f => f.FriendPlayerEntity)
            .Where(f => f.PlayerEntityId == playerId)
            .ToListAsync(ct);

        foreach (var entity in friendEntities)
        {
            _friends[entity.FriendPlayerEntityId] = new MessengerFriendSnapshot
            {
                PlayerId = PlayerId.Parse(entity.FriendPlayerEntityId),
                Name = entity.FriendPlayerEntity.Name,
                Gender = entity.FriendPlayerEntity.Gender,
                Online = false,
                FollowingAllowed = true,
                Figure = entity.FriendPlayerEntity.Figure,
                CategoryId = entity.MessengerCategoryEntityId ?? 0,
                Motto = entity.FriendPlayerEntity.Motto ?? string.Empty,
                LastAccess = entity.FriendPlayerEntity.UpdatedAt.ToString("dd-MM-yyyy HH:mm:ss"),
                RealName = string.Empty,
                FacebookId = string.Empty,
                PersistedMessageUser = true,
                VipMember = false,
                PocketHabboUser = false,
                RelationshipStatus = (short)entity.RelationType,
            };
        }

        // Load incoming friend requests
        var requestEntities = await dbCtx
            .MessengerRequests.AsNoTracking()
            .Include(r => r.PlayerEntity)
            .Where(r => r.RequestedPlayerEntityId == playerId)
            .ToListAsync(ct);

        foreach (var entity in requestEntities)
        {
            _incomingRequests[entity.PlayerEntityId] = new FriendRequestSnapshot
            {
                RequestId = entity.PlayerEntityId,
                RequesterName = entity.PlayerEntity.Name,
                FigureString = entity.PlayerEntity.Figure,
                RequesterUserId = PlayerId.Parse(entity.PlayerEntityId),
            };
        }

        // Load blocked users
        var blockedEntities = await dbCtx
            .MessengerBlocked.AsNoTracking()
            .Where(b => b.PlayerEntityId == playerId)
            .Select(b => b.BlockedPlayerEntityId)
            .ToListAsync(ct);

        foreach (var id in blockedEntities)
            _blockedUserIds.Add(id);

        // Load ignored users
        var ignoredEntities = await dbCtx
            .MessengerIgnored.AsNoTracking()
            .Where(i => i.PlayerEntityId == playerId)
            .Select(i => i.IgnoredPlayerEntityId)
            .ToListAsync(ct);

        foreach (var id in ignoredEntities)
            _ignoredUserIds.Add(id);

        // Load categories
        var categoryEntities = await dbCtx
            .MessengerCategories.AsNoTracking()
            .Where(c => c.PlayerEntityId == playerId)
            .ToListAsync(ct);

        foreach (var entity in categoryEntities)
            _categories.Add(new FriendCategorySnapshot { Id = entity.Id, Name = entity.Name });

        // Batch-check online status for all friends in parallel (avoids N+1 sequential grain calls)
        if (_friends.Count > 0)
        {
            var onlineTasks = _friends
                .Keys.Select(async friendId =>
                {
                    var isOnline = await IsPlayerOnlineAsync(PlayerId.Parse(friendId));
                    return (friendId, isOnline);
                })
                .ToList();

            var results = await Task.WhenAll(onlineTasks);

            foreach (var (friendId, isOnline) in results)
            {
                if (isOnline && _friends.TryGetValue(friendId, out var friend))
                    _friends[friendId] = friend with { Online = true };
            }
        }

        _isLoaded = true;
    }

    #region Initialization

    public Task<List<MessengerFriendSnapshot>> GetFriendsAsync(CancellationToken ct) =>
        Task.FromResult(_friends.Values.ToList());

    public Task<List<FriendCategorySnapshot>> GetCategoriesAsync(CancellationToken ct) =>
        Task.FromResult(_categories.ToList());

    public Task<List<FriendRequestSnapshot>> GetFriendRequestsAsync(CancellationToken ct) =>
        Task.FromResult(_incomingRequests.Values.ToList());

    public Task<List<int>> GetBlockedUserIdsAsync(CancellationToken ct) =>
        Task.FromResult(_blockedUserIds.ToList());

    public Task<List<int>> GetIgnoredUserIdsAsync(CancellationToken ct) =>
        Task.FromResult(_ignoredUserIds.ToList());

    #endregion

    #region Friend Requests

    public async Task<FriendListErrorCodeType?> SendFriendRequestAsync(
        PlayerId targetPlayerId,
        string senderName,
        string senderFigure,
        int friendLimit,
        CancellationToken ct
    )
    {
        var myId = (int)_playerId;
        var targetId = (int)targetPlayerId;

        // Check own friend limit
        if (_friends.Count >= friendLimit)
            return FriendListErrorCodeType.YouHitFriendLimit;

        // Check if blocked by them
        var targetGrain = grainFactory.GetMessengerGrain(targetPlayerId);
        if (await targetGrain.IsBlockedAsync(_playerId))
            return FriendListErrorCodeType.BlockedByThem;

        // Check if we blocked them
        if (_blockedUserIds.Contains(targetId))
            return FriendListErrorCodeType.BlockedByYou;

        // Check existing friendship
        if (_friends.ContainsKey(targetId))
            return null; // Already friends, silently ignore

        // Check target friend limit
        var targetFriendCount = await targetGrain.GetFriendCountAsync();
        if (targetFriendCount >= friendLimit)
            return FriendListErrorCodeType.TheyHitFriendLimit;

        // Check existing pending request in either direction
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var existingRequest = await dbCtx
            .MessengerRequests.AsNoTracking()
            .AnyAsync(
                r =>
                    (r.PlayerEntityId == myId && r.RequestedPlayerEntityId == targetId)
                    || (r.PlayerEntityId == targetId && r.RequestedPlayerEntityId == myId),
                ct
            );

        if (existingRequest)
            return null; // Already a pending request, silently ignore

        // Create the request
        dbCtx.MessengerRequests.Add(
            new MessengerRequestEntity
            {
                PlayerEntityId = myId,
                RequestedPlayerEntityId = targetId,
                PlayerEntity = null!,
                RequestedPlayerEntity = null!,
            }
        );

        await dbCtx.SaveChangesAsync(ct);

        // Notify the target
        var requestSnapshot = new FriendRequestSnapshot
        {
            RequestId = myId,
            RequesterName = senderName,
            FigureString = senderFigure,
            RequesterUserId = _playerId,
        };

        await targetGrain.ReceiveFriendRequestAsync(requestSnapshot);

        return null; // Success
    }

    public async Task ReceiveFriendRequestAsync(FriendRequestSnapshot request)
    {
        _incomingRequests[request.RequesterUserId] = request;

        // Send notification to the player if online
        try
        {
            var presence = grainFactory.GetPlayerPresenceGrain(_playerId);
            await presence.SendComposerAsync(
                new NewFriendRequestMessageComposer { Request = request }
            );
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Failed to send friend request notification to player {PlayerId}",
                _playerId
            );
        }
    }

    public async Task<(
        List<AcceptFriendFailureSnapshot> Failures,
        List<FriendListUpdateSnapshot> Updates
    )> AcceptFriendRequestsAsync(List<int> requestIds, int friendLimit, CancellationToken ct)
    {
        var failures = new List<AcceptFriendFailureSnapshot>();
        var updates = new List<FriendListUpdateSnapshot>();
        var myId = (int)_playerId;

        // Fetch our own summary once, outside the loop — it's the same for every iteration
        var myPlayerGrain = grainFactory.GetPlayerGrain(_playerId);
        var mySummary = await myPlayerGrain.GetSummaryAsync(ct);

        foreach (var requesterId in requestIds)
        {
            // Check if we actually have this request
            if (!_incomingRequests.ContainsKey(requesterId))
            {
                failures.Add(
                    new AcceptFriendFailureSnapshot
                    {
                        SenderId = requesterId,
                        ErrorCode = FriendListErrorCodeType.FriendRequestNotFound,
                    }
                );
                continue;
            }

            // Check our friend limit
            if (_friends.Count >= friendLimit)
            {
                failures.Add(
                    new AcceptFriendFailureSnapshot
                    {
                        SenderId = requesterId,
                        ErrorCode = FriendListErrorCodeType.YouHitFriendLimit,
                    }
                );
                continue;
            }

            // Check their friend limit
            var requesterGrain = grainFactory.GetMessengerGrain(requesterId);
            var requesterFriendCount = await requesterGrain.GetFriendCountAsync();
            if (requesterFriendCount >= friendLimit)
            {
                failures.Add(
                    new AcceptFriendFailureSnapshot
                    {
                        SenderId = requesterId,
                        ErrorCode = FriendListErrorCodeType.TheyHitFriendLimit,
                    }
                );
                continue;
            }

            await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

            // Track-remove the request so delete + friendship insert go through one SaveChanges
            var requestEntity = await dbCtx.MessengerRequests.FirstOrDefaultAsync(
                r => r.PlayerEntityId == requesterId && r.RequestedPlayerEntityId == myId,
                ct
            );

            if (requestEntity != null)
                dbCtx.MessengerRequests.Remove(requestEntity);

            // Create bidirectional friendship
            dbCtx.MessengerFriends.Add(
                new MessengerFriendEntity
                {
                    PlayerEntityId = myId,
                    FriendPlayerEntityId = requesterId,
                    RelationType = MessengerFriendRelationType.Zero,
                    PlayerEntity = null!,
                    FriendPlayerEntity = null!,
                }
            );

            dbCtx.MessengerFriends.Add(
                new MessengerFriendEntity
                {
                    PlayerEntityId = requesterId,
                    FriendPlayerEntityId = myId,
                    RelationType = MessengerFriendRelationType.Zero,
                    PlayerEntity = null!,
                    FriendPlayerEntity = null!,
                }
            );

            await dbCtx.SaveChangesAsync(ct);

            // Remove from local request cache
            _incomingRequests.Remove(requesterId);

            // Get requester's player info + online status in parallel (independent grain calls)
            var requesterPlayerGrain = grainFactory.GetPlayerGrain(PlayerId.Parse(requesterId));
            var summaryTask = requesterPlayerGrain.GetSummaryAsync(ct);
            var onlineTask = IsPlayerOnlineAsync(PlayerId.Parse(requesterId));
            await Task.WhenAll(summaryTask, onlineTask);

            var requesterSummary = await summaryTask;
            var requesterIsOnline = await onlineTask;

            // Add to our local friends cache
            var friendSnapshotForUs = new MessengerFriendSnapshot
            {
                PlayerId = PlayerId.Parse(requesterId),
                Name = requesterSummary.Name,
                Gender = requesterSummary.Gender,
                Online = requesterIsOnline,
                FollowingAllowed = true,
                Figure = requesterSummary.Figure,
                CategoryId = 0,
                Motto = requesterSummary.Motto,
                LastAccess = requesterSummary.CreatedAt.ToString("dd-MM-yyyy HH:mm:ss"),
                RealName = string.Empty,
                FacebookId = string.Empty,
                PersistedMessageUser = true,
                VipMember = false,
                PocketHabboUser = false,
                RelationshipStatus = 0,
            };
            _friends[requesterId] = friendSnapshotForUs;

            // Collect update for the caller (handler will send it to the client)
            updates.Add(
                new FriendListUpdateSnapshot
                {
                    ActionType = FriendListUpdateActionType.Added,
                    FriendId = requesterId,
                    Friend = friendSnapshotForUs,
                }
            );

            // Build snapshot of ourselves for the requester (using pre-fetched summary)
            var friendSnapshotForThem = new MessengerFriendSnapshot
            {
                PlayerId = _playerId,
                Name = mySummary.Name,
                Gender = mySummary.Gender,
                Online = true, // We must be online to accept
                FollowingAllowed = true,
                Figure = mySummary.Figure,
                CategoryId = 0,
                Motto = mySummary.Motto,
                LastAccess = mySummary.CreatedAt.ToString("dd-MM-yyyy HH:mm:ss"),
                RealName = string.Empty,
                FacebookId = string.Empty,
                PersistedMessageUser = true,
                VipMember = false,
                PocketHabboUser = false,
                RelationshipStatus = 0,
            };

            // Notify the requester of the new friendship
            LogAndForget(
                requesterGrain.OnFriendAcceptedAsync(friendSnapshotForThem),
                nameof(OnFriendAcceptedAsync),
                requesterId
            );
        }

        return (failures, updates);
    }

    public async Task DeclineFriendRequestsAsync(
        List<int>? requestIds,
        bool declineAll,
        CancellationToken ct
    )
    {
        var myId = (int)_playerId;

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        if (declineAll)
        {
            await dbCtx
                .MessengerRequests.Where(r => r.RequestedPlayerEntityId == myId)
                .ExecuteDeleteAsync(ct);

            _incomingRequests.Clear();
        }
        else if (requestIds is { Count: > 0 })
        {
            await dbCtx
                .MessengerRequests.Where(r =>
                    r.RequestedPlayerEntityId == myId && requestIds.Contains(r.PlayerEntityId)
                )
                .ExecuteDeleteAsync(ct);

            foreach (var id in requestIds)
                _incomingRequests.Remove(id);
        }
    }

    #endregion

    #region Friend Management

    public async Task RemoveFriendsAsync(List<int> friendIds, CancellationToken ct)
    {
        var myId = (int)_playerId;

        // Filter to only friends we actually have
        var validFriendIds = friendIds.Where(id => _friends.ContainsKey(id)).ToList();

        if (validFriendIds.Count == 0)
            return;

        // Batch-delete all friendship rows in a single query instead of N+1 DB calls
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);
        await dbCtx
            .MessengerFriends.Where(f =>
                (f.PlayerEntityId == myId && validFriendIds.Contains(f.FriendPlayerEntityId))
                || (validFriendIds.Contains(f.PlayerEntityId) && f.FriendPlayerEntityId == myId)
            )
            .ExecuteDeleteAsync(ct);

        // Build all removal updates and update local cache
        var removeUpdates = new List<FriendListUpdateSnapshot>();
        foreach (var friendId in validFriendIds)
        {
            _friends.Remove(friendId);

            removeUpdates.Add(
                new FriendListUpdateSnapshot
                {
                    ActionType = FriendListUpdateActionType.Removed,
                    FriendId = friendId,
                }
            );

            // Notify the other side (fire-and-forget)
            var friendGrain = grainFactory.GetMessengerGrain(friendId);
            LogAndForget(
                friendGrain.OnFriendRemovedAsync(_playerId),
                nameof(OnFriendRemovedAsync),
                friendId
            );
        }

        // Send all removal updates to ourselves in a single composer call
        try
        {
            var presence = grainFactory.GetPlayerPresenceGrain(_playerId);
            await presence.SendComposerAsync(
                new FriendListUpdateMessageComposer
                {
                    FriendCategories = [],
                    Updates = removeUpdates,
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Failed to send friend removal updates to player {PlayerId}",
                _playerId
            );
        }
    }

    public async Task SetRelationshipStatusAsync(int friendId, int status, CancellationToken ct)
    {
        if (!_friends.ContainsKey(friendId))
            return;

        var myId = (int)_playerId;
        var relationType = (MessengerFriendRelationType)status;

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        await dbCtx
            .MessengerFriends.Where(f =>
                f.PlayerEntityId == myId && f.FriendPlayerEntityId == friendId
            )
            .ExecuteUpdateAsync(up => up.SetProperty(f => f.RelationType, relationType), ct);

        // Update local cache
        if (_friends.TryGetValue(friendId, out var existing))
        {
            _friends[friendId] = existing with { RelationshipStatus = (short)status };
        }
    }

    public Task<List<RelationshipStatusEntrySnapshot>> GetRelationshipStatusInfoAsync(
        CancellationToken ct
    )
    {
        var entries = new List<RelationshipStatusEntrySnapshot>();

        var grouped = _friends
            .Values.Where(f => f.RelationshipStatus > 0)
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

    #endregion

    #region Friend State Queries

    public Task<bool> IsFriendAsync(PlayerId playerId) =>
        Task.FromResult(_friends.ContainsKey(playerId));

    public Task<bool> IsFriendRequestSentAsync(PlayerId playerId) =>
        Task.FromResult(_incomingRequests.ContainsKey(playerId));

    public Task<int> GetFriendCountAsync() => Task.FromResult(_friends.Count);

    #endregion

    #region Blocking

    public async Task BlockUserAsync(PlayerId targetId, CancellationToken ct)
    {
        var myId = (int)_playerId;
        var target = (int)targetId;

        if (_blockedUserIds.Contains(target))
            return;

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        dbCtx.MessengerBlocked.Add(
            new MessengerBlockedEntity
            {
                PlayerEntityId = myId,
                BlockedPlayerEntityId = target,
                PlayerEntity = null!,
                BlockedPlayerEntity = null!,
            }
        );

        await dbCtx.SaveChangesAsync(ct);
        _blockedUserIds.Add(target);

        // If the blocked user is a friend, remove the friendship
        if (_friends.ContainsKey(target))
            await RemoveFriendsAsync([target], ct);
    }

    public async Task UnblockUserAsync(PlayerId targetId, CancellationToken ct)
    {
        var myId = (int)_playerId;
        var target = (int)targetId;

        if (!_blockedUserIds.Contains(target))
            return;

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        await dbCtx
            .MessengerBlocked.Where(b =>
                b.PlayerEntityId == myId && b.BlockedPlayerEntityId == target
            )
            .ExecuteDeleteAsync(ct);

        _blockedUserIds.Remove(target);
    }

    public Task<bool> IsBlockedAsync(PlayerId targetId) =>
        Task.FromResult(_blockedUserIds.Contains(targetId));

    public Task<bool> IsBlockedByAsync(PlayerId targetId) =>
        // This checks if the OTHER player has blocked US
        // We need to ask the other player's grain
        Task.FromResult(false); // Handled via grain-to-grain call
    #endregion

    #region Ignoring

    public async Task<int> IgnoreUserAsync(
        PlayerId targetId,
        int maxIgnoreCapacity,
        CancellationToken ct
    )
    {
        var myId = (int)_playerId;
        var target = (int)targetId;

        if (_ignoredUserIds.Contains(target))
            return 0; // Already ignored, fail

        int result;

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        if (_ignoredUserIds.Count >= maxIgnoreCapacity)
        {
            // Evict the oldest entry
            var oldest = await dbCtx
                .MessengerIgnored.Where(i => i.PlayerEntityId == myId)
                .OrderBy(i => i.Id)
                .FirstOrDefaultAsync(ct);

            if (oldest is not null)
            {
                dbCtx.MessengerIgnored.Remove(oldest);
                _ignoredUserIds.Remove(oldest.IgnoredPlayerEntityId);
            }

            result = 2; // Success, oldest evicted
        }
        else
        {
            result = 1; // Success
        }

        dbCtx.MessengerIgnored.Add(
            new MessengerIgnoredEntity
            {
                PlayerEntityId = myId,
                IgnoredPlayerEntityId = target,
                PlayerEntity = null!,
                IgnoredPlayerEntity = null!,
            }
        );

        await dbCtx.SaveChangesAsync(ct);
        _ignoredUserIds.Add(target);

        return result;
    }

    public async Task UnignoreUserAsync(PlayerId targetId, CancellationToken ct)
    {
        var myId = (int)_playerId;
        var target = (int)targetId;

        if (!_ignoredUserIds.Contains(target))
            return;

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        await dbCtx
            .MessengerIgnored.Where(i =>
                i.PlayerEntityId == myId && i.IgnoredPlayerEntityId == target
            )
            .ExecuteDeleteAsync(ct);

        _ignoredUserIds.Remove(target);
    }

    #endregion

    #region Messaging

    public async Task<string> SendMessageAsync(
        PlayerId recipientId,
        string message,
        int confirmationId,
        string senderName,
        string senderFigure,
        CancellationToken ct
    )
    {
        var myId = (int)_playerId;
        var targetId = (int)recipientId;

        // Store message in DB (for offline delivery if recipient is offline)
        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var now = DateTime.UtcNow;

        var messageEntity = new MessengerMessageEntity
        {
            SenderPlayerEntityId = myId,
            ReceiverPlayerEntityId = targetId,
            Message = message,
            Timestamp = now,
            SenderPlayerEntity = null!,
            ReceiverPlayerEntity = null!,
        };

        dbCtx.MessengerMessages.Add(messageEntity);
        await dbCtx.SaveChangesAsync(ct);

        var sessionMsgId = (_nextSessionMessageId++).ToString();

        // Record in sender's session history
        AddToSessionHistory(
            targetId,
            new MessageHistoryEntrySnapshot
            {
                SenderId = _playerId,
                SenderName = senderName,
                SenderFigure = senderFigure,
                Message = message,
                SecondsSinceSent = 0,
                MessageId = sessionMsgId,
            }
        );

        // Send to recipient (fire-and-forget to avoid cross-grain deadlock)
        // confirmationId must be 0 for the recipient — the client uses confirmationId > 0
        // to route the packet to onConfirmOwnChatMessage (sender echo) instead of
        // recordChatMessage (actual incoming message).
        var recipientGrain = grainFactory.GetMessengerGrain(recipientId);
        LogAndForget(
            recipientGrain.ReceiveMessageAsync(
                myId, // chatId = sender's ID for the recipient
                message,
                0,
                sessionMsgId,
                0, // confirmationId = 0 → client treats this as a received message
                myId,
                senderName,
                senderFigure,
                messageEntity.Id // DB id so recipient can delete after delivery
            ),
            nameof(ReceiveMessageAsync),
            targetId
        );

        return sessionMsgId;
    }

    public async Task ReceiveMessageAsync(
        int chatId,
        string messageText,
        int secondsSinceSent,
        string messageId,
        int confirmationId,
        int senderId,
        string senderName,
        string senderFigure,
        int dbMessageId = 0
    )
    {
        // If we're not online, the message stays in DB for delivery on next login
        if (!_isOnline)
            return;

        // Assign a session-local messageId for the recipient's history
        var sessionMsgId = (_nextSessionMessageId++).ToString();

        // Record in recipient's (our) session history
        AddToSessionHistory(
            senderId,
            new MessageHistoryEntrySnapshot
            {
                SenderId = PlayerId.Parse(senderId),
                SenderName = senderName,
                SenderFigure = senderFigure,
                Message = messageText,
                SecondsSinceSent = secondsSinceSent,
                MessageId = sessionMsgId,
            }
        );

        var presence = grainFactory.GetPlayerPresenceGrain(_playerId);
        await presence.SendComposerAsync(
            new NewConsoleMessageMessageComposer
            {
                ChatId = chatId,
                Message = messageText,
                SecondsSinceSent = secondsSinceSent,
                MessageId = sessionMsgId,
                ConfirmationId = confirmationId,
                SenderId = senderId,
                SenderName = senderName,
                SenderFigure = senderFigure,
            }
        );

        // Queue delivered-flag update — flushed periodically by timer to avoid per-message DB writes
        if (dbMessageId > 0)
            _pendingDeliveredIds.Add(dbMessageId);
    }

    public Task<List<MessageHistoryEntrySnapshot>> GetMessageHistoryAsync(
        int chatPartnerId,
        string lastMessageId,
        int pageSize,
        CancellationToken ct
    )
    {
        // Session-based history — only messages from this login session
        if (!_sessionMessages.TryGetValue(chatPartnerId, out var entries) || entries.Count == 0)
            return Task.FromResult(new List<MessageHistoryEntrySnapshot>());

        IEnumerable<MessageHistoryEntrySnapshot> result;

        // Cursor-based pagination: return entries before the given messageId
        if (!string.IsNullOrEmpty(lastMessageId) && int.TryParse(lastMessageId, out var lastId))
        {
            // Session message IDs are sequential ints starting from 1
            // Find the index of the entry with this messageId and return entries before it
            var idx = entries.FindIndex(e => e.MessageId == lastMessageId);
            if (idx > 0)
                result = entries.Take(idx);
            else
                return Task.FromResult(new List<MessageHistoryEntrySnapshot>());
        }
        else
        {
            // No cursor — return the most recent entries
            result = entries;
        }

        // Return up to pageSize entries, newest first (reverse order from the end)
        return Task.FromResult(result.Reverse().Take(pageSize).Reverse().ToList());
    }

    /// <summary>
    /// Delivers unread offline messages from DB and marks them as delivered.
    /// Called during messenger init after friend list is sent.
    /// </summary>
    public async Task DeliverOfflineMessagesAsync(CancellationToken ct)
    {
        var myId = (int)_playerId;

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var offlineMessages = await dbCtx
            .MessengerMessages.Include(m => m.SenderPlayerEntity)
            .Where(m => m.ReceiverPlayerEntityId == myId && !m.Delivered)
            .OrderBy(m => m.Id)
            .ToListAsync(ct);

        if (offlineMessages.Count == 0)
            return;

        var now = DateTime.UtcNow;
        var presence = grainFactory.GetPlayerPresenceGrain(_playerId);

        foreach (var msg in offlineMessages)
        {
            var secondsSince = (int)(now - msg.Timestamp).TotalSeconds;
            var sessionMsgId = (_nextSessionMessageId++).ToString();

            // Add to session history
            AddToSessionHistory(
                msg.SenderPlayerEntityId,
                new MessageHistoryEntrySnapshot
                {
                    SenderId = PlayerId.Parse(msg.SenderPlayerEntityId),
                    SenderName = msg.SenderPlayerEntity.Name,
                    SenderFigure = msg.SenderPlayerEntity.Figure,
                    Message = msg.Message,
                    SecondsSinceSent = secondsSince,
                    MessageId = sessionMsgId,
                }
            );

            // Push to client as NewConsoleMessage
            await presence.SendComposerAsync(
                new NewConsoleMessageMessageComposer
                {
                    ChatId = msg.SenderPlayerEntityId,
                    Message = msg.Message,
                    SecondsSinceSent = secondsSince,
                    MessageId = sessionMsgId,
                    ConfirmationId = 0,
                    SenderId = msg.SenderPlayerEntityId,
                    SenderName = msg.SenderPlayerEntity.Name,
                    SenderFigure = msg.SenderPlayerEntity.Figure,
                }
            );
        }

        // Queue delivered-flag updates — flushed by the periodic timer
        foreach (var msg in offlineMessages)
            _pendingDeliveredIds.Add(msg.Id);
    }

    private void AddToSessionHistory(int chatPartnerId, MessageHistoryEntrySnapshot entry)
    {
        if (!_sessionMessages.TryGetValue(chatPartnerId, out var list))
        {
            list = [];
            _sessionMessages[chatPartnerId] = list;
        }

        list.Add(entry);

        // Evict oldest entries when conversation exceeds the limit
        if (list.Count > MaxSessionMessagesPerConversation)
            list.RemoveAt(0);
    }

    #endregion

    #region Room Invites

    public async Task ReceiveRoomInviteAsync(int senderId, string message)
    {
        try
        {
            var presence = grainFactory.GetPlayerPresenceGrain(_playerId);
            await presence.SendComposerAsync(
                new RoomInviteMessageComposer { SenderId = senderId, Message = message }
            );
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Failed to deliver room invite from {SenderId} to player {PlayerId}",
                senderId,
                _playerId
            );
        }
    }

    #endregion

    #region Follow

    public async Task<(
        bool Success,
        int RoomId,
        FollowFriendErrorCodeType? Error
    )> FollowFriendAsync(PlayerId targetId, CancellationToken ct)
    {
        if (!_friends.ContainsKey((int)targetId))
            return (false, 0, FollowFriendErrorCodeType.NotFriend);

        // Check if target is online
        var isOnline = await IsPlayerOnlineAsync(targetId);
        if (!isOnline)
            return (false, 0, FollowFriendErrorCodeType.Offline);

        // Check if target is in a room
        var targetPresence = grainFactory.GetPlayerPresenceGrain(targetId);
        var activeRoom = await targetPresence.GetActiveRoomAsync();

        if (activeRoom.RoomId <= 0)
            return (false, 0, FollowFriendErrorCodeType.HotelView);

        return (true, (int)activeRoom.RoomId.Value, null);
    }

    #endregion

    #region Online/Offline Presence

    public async Task NotifyOnlineAsync(CancellationToken ct)
    {
        _isOnline = true;

        // Clear any stale session state from a previous login
        _sessionMessages.Clear();
        _nextSessionMessageId = 1;

        var myPlayerGrain = grainFactory.GetPlayerGrain(_playerId);
        var mySummary = await myPlayerGrain.GetSummaryAsync(ct);

        var mySnapshot = new MessengerFriendSnapshot
        {
            PlayerId = _playerId,
            Name = mySummary.Name,
            Gender = mySummary.Gender,
            Online = true,
            FollowingAllowed = true,
            Figure = mySummary.Figure,
            CategoryId = 0,
            Motto = mySummary.Motto,
            LastAccess = mySummary.CreatedAt.ToString("dd-MM-yyyy HH:mm:ss"),
            RealName = string.Empty,
            FacebookId = string.Empty,
            PersistedMessageUser = true,
            VipMember = false,
            PocketHabboUser = false,
            RelationshipStatus = 0,
        };

        var update = new FriendListUpdateSnapshot
        {
            ActionType = FriendListUpdateActionType.Updated,
            FriendId = _playerId,
            Friend = mySnapshot,
        };

        foreach (var friendId in _friends.Keys)
        {
            var friendGrain = grainFactory.GetMessengerGrain(friendId);
            LogAndForget(
                friendGrain.ReceiveFriendUpdateAsync(update),
                nameof(ReceiveFriendUpdateAsync),
                friendId
            );
        }
    }

    public async Task NotifyOfflineAsync(CancellationToken ct)
    {
        _isOnline = false;

        // Clear session-based message history
        _sessionMessages.Clear();
        _nextSessionMessageId = 1;

        var myPlayerGrain = grainFactory.GetPlayerGrain(_playerId);
        var mySummary = await myPlayerGrain.GetSummaryAsync(ct);

        var mySnapshot = new MessengerFriendSnapshot
        {
            PlayerId = _playerId,
            Name = mySummary.Name,
            Gender = mySummary.Gender,
            Online = false,
            FollowingAllowed = true,
            Figure = mySummary.Figure,
            CategoryId = 0,
            Motto = mySummary.Motto,
            LastAccess = mySummary.CreatedAt.ToString("dd-MM-yyyy HH:mm:ss"),
            RealName = string.Empty,
            FacebookId = string.Empty,
            PersistedMessageUser = true,
            VipMember = false,
            PocketHabboUser = false,
            RelationshipStatus = 0,
        };

        var update = new FriendListUpdateSnapshot
        {
            ActionType = FriendListUpdateActionType.Updated,
            FriendId = _playerId,
            Friend = mySnapshot,
        };

        foreach (var friendId in _friends.Keys)
        {
            var friendGrain = grainFactory.GetMessengerGrain(friendId);
            LogAndForget(
                friendGrain.ReceiveFriendUpdateAsync(update),
                nameof(ReceiveFriendUpdateAsync),
                friendId
            );
        }
    }

    #endregion

    #region Friend Updates

    public async Task ReceiveFriendUpdateAsync(FriendListUpdateSnapshot update)
    {
        // Update local cache if this is an update for an existing friend
        if (
            update is { ActionType: FriendListUpdateActionType.Updated, Friend: not null }
            && _friends.ContainsKey(update.FriendId)
        )
        {
            // Preserve our local category/relationship settings
            var existing = _friends[update.FriendId];
            _friends[update.FriendId] = update.Friend with
            {
                CategoryId = existing.CategoryId,
                RelationshipStatus = existing.RelationshipStatus,
            };
        }

        // Forward to the player's client
        try
        {
            var presence = grainFactory.GetPlayerPresenceGrain(_playerId);
            await presence.SendComposerAsync(
                new FriendListUpdateMessageComposer { FriendCategories = [], Updates = [update] }
            );
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to forward friend update to player {PlayerId}", _playerId);
        }
    }

    public async Task OnFriendRemovedAsync(PlayerId friendId)
    {
        _friends.Remove((int)friendId);

        var removeUpdate = new FriendListUpdateSnapshot
        {
            ActionType = FriendListUpdateActionType.Removed,
            FriendId = friendId,
        };

        try
        {
            var presence = grainFactory.GetPlayerPresenceGrain(_playerId);
            await presence.SendComposerAsync(
                new FriendListUpdateMessageComposer
                {
                    FriendCategories = [],
                    Updates = [removeUpdate],
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Failed to send friend removal notification to player {PlayerId}",
                _playerId
            );
        }
    }

    public async Task OnFriendAcceptedAsync(MessengerFriendSnapshot friendSnapshot)
    {
        _friends[friendSnapshot.PlayerId] = friendSnapshot;

        var addedUpdate = new FriendListUpdateSnapshot
        {
            ActionType = FriendListUpdateActionType.Added,
            FriendId = friendSnapshot.PlayerId,
            Friend = friendSnapshot,
        };

        try
        {
            var presence = grainFactory.GetPlayerPresenceGrain(_playerId);
            await presence.SendComposerAsync(
                new FriendListUpdateMessageComposer
                {
                    FriendCategories = [],
                    Updates = [addedUpdate],
                }
            );
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Failed to send friend accepted notification to player {PlayerId}",
                _playerId
            );
        }
    }

    #endregion

    #region Habbo Search

    public async Task<(
        List<MessengerSearchResultSnapshot> Friends,
        List<MessengerSearchResultSnapshot> Others
    )> SearchPlayersAsync(string query, int searchLimit, CancellationToken ct)
    {
        var friends = new List<MessengerSearchResultSnapshot>();
        var others = new List<MessengerSearchResultSnapshot>();

        if (string.IsNullOrWhiteSpace(query))
            return (friends, others);

        await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);

        var results = await dbCtx
            .Players.AsNoTracking()
            .Where(p => EF.Functions.Like(p.Name, $"%{query}%"))
            .Take(searchLimit)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.Motto,
                p.Figure,
                p.Gender,
            })
            .ToListAsync(ct);

        var filtered = results.Where(player => player.Id != (int)_playerId).ToList();

        // Batch-check online status in parallel instead of sequential N+1
        var onlineTasks = filtered
            .Select(async p =>
            {
                var isOnline = await IsPlayerOnlineAsync(PlayerId.Parse(p.Id));
                return (p, isOnline);
            })
            .ToList();
        var onlineResults = await Task.WhenAll(onlineTasks);

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

            if (_friends.ContainsKey(player.Id))
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

    #endregion

    #region Persistence

    private async Task FlushDeliveredMessagesAsync(CancellationToken ct)
    {
        if (_pendingDeliveredIds.Count == 0)
            return;

        var batch = _pendingDeliveredIds.ToList();
        _pendingDeliveredIds.Clear();

        try
        {
            await using var dbCtx = await dbCtxFactory.CreateDbContextAsync(ct);
            await dbCtx
                .MessengerMessages.Where(m => batch.Contains(m.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(m => m.Delivered, true), ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to flush {Count} delivered message flags for player {PlayerId}",
                batch.Count,
                _playerId
            );
        }
    }

    #endregion

    #region Helpers

    private async Task<bool> IsPlayerOnlineAsync(PlayerId playerId)
    {
        try
        {
            var presence = grainFactory.GetPlayerPresenceGrain(playerId);
            return await presence.HasActiveSessionAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to check online status for player {PlayerId}", playerId);
            return false;
        }
    }

    private void LogAndForget(Task task, string operation, int targetId)
    {
        _ = task.ContinueWith(
            t =>
                logger.LogDebug(
                    t.Exception?.GetBaseException(),
                    "Fire-and-forget {Operation} failed for target {TargetId} from player {PlayerId}",
                    operation,
                    targetId,
                    _playerId
                ),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default
        );
    }

    #endregion
}
