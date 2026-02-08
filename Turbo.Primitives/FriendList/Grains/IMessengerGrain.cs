using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.FriendList.Enums;
using Turbo.Primitives.Players;
using Turbo.Primitives.Snapshots.FriendList;

namespace Turbo.Primitives.FriendList.Grains;

public interface IMessengerGrain : IGrainWithIntegerKey
{
    // Initialization
    Task<List<MessengerFriendSnapshot>> GetFriendsAsync(CancellationToken ct);
    Task<List<FriendCategorySnapshot>> GetCategoriesAsync(CancellationToken ct);
    Task<List<FriendRequestSnapshot>> GetFriendRequestsAsync(CancellationToken ct);
    Task<List<int>> GetBlockedUserIdsAsync(CancellationToken ct);
    Task<List<int>> GetIgnoredUserIdsAsync(CancellationToken ct);

    // Friend Requests
    Task<FriendListErrorCodeType?> SendFriendRequestAsync(
        PlayerId targetPlayerId,
        string senderName,
        string senderFigure,
        int friendLimit,
        CancellationToken ct
    );

    Task ReceiveFriendRequestAsync(FriendRequestSnapshot request);

    Task<(
        List<AcceptFriendFailureSnapshot> Failures,
        List<FriendListUpdateSnapshot> Updates
    )> AcceptFriendRequestsAsync(
        List<int> requestIds,
        int friendLimit,
        CancellationToken ct
    );

    Task DeclineFriendRequestsAsync(List<int>? requestIds, bool declineAll, CancellationToken ct);

    // Friend Management
    Task RemoveFriendsAsync(List<int> friendIds, CancellationToken ct);
    Task SetRelationshipStatusAsync(int friendId, int status, CancellationToken ct);
    Task<List<RelationshipStatusEntrySnapshot>> GetRelationshipStatusInfoAsync(CancellationToken ct);

    // Friend state queries
    Task<bool> IsFriendAsync(PlayerId playerId);
    Task<bool> IsFriendRequestSentAsync(PlayerId playerId);
    Task<int> GetFriendCountAsync();

    // Blocking
    Task BlockUserAsync(PlayerId targetId, CancellationToken ct);
    Task UnblockUserAsync(PlayerId targetId, CancellationToken ct);
    Task<bool> IsBlockedAsync(PlayerId targetId);
    Task<bool> IsBlockedByAsync(PlayerId targetId);

    // Ignoring
    Task<int> IgnoreUserAsync(PlayerId targetId, CancellationToken ct);
    Task UnignoreUserAsync(PlayerId targetId, CancellationToken ct);

    // Messaging
    Task<string> SendMessageAsync(
        PlayerId recipientId,
        string message,
        int confirmationId,
        string senderName,
        string senderFigure,
        CancellationToken ct
    );

    Task ReceiveMessageAsync(
        int chatId,
        string messageText,
        int secondsSinceSent,
        string messageId,
        int confirmationId,
        int senderId,
        string senderName,
        string senderFigure,
        int dbMessageId = 0
    );

    Task<List<MessageHistoryEntrySnapshot>> GetMessageHistoryAsync(
        int chatPartnerId,
        string lastMessageId,
        CancellationToken ct
    );

    // Offline message delivery
    Task DeliverOfflineMessagesAsync(CancellationToken ct);

    // Room Invites
    Task ReceiveRoomInviteAsync(int senderId, string message);

    // Follow
    Task<(bool Success, int RoomId, FollowFriendErrorCodeType? Error)> FollowFriendAsync(
        PlayerId targetId,
        CancellationToken ct
    );

    // Online/Offline presence
    Task NotifyOnlineAsync(CancellationToken ct);
    Task NotifyOfflineAsync(CancellationToken ct);

    // Friend list updates push
    Task ReceiveFriendUpdateAsync(FriendListUpdateSnapshot update);

    // Habbo search
    Task<(List<MessengerSearchResultSnapshot> Friends, List<MessengerSearchResultSnapshot> Others)> SearchPlayersAsync(
        string query,
        CancellationToken ct
    );

    // Remove friendship (called by the other side)
    Task OnFriendRemovedAsync(PlayerId friendId);

    // Accept friendship (called by the other side)
    Task OnFriendAcceptedAsync(MessengerFriendSnapshot friendSnapshot);
}
