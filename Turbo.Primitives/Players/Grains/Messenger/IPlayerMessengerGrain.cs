using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Players.Enums.Messenger;
using Turbo.Primitives.Players.Messenger;
using Turbo.Primitives.Players.Snapshots;
using Turbo.Primitives.Players.Snapshots.Messenger;

namespace Turbo.Primitives.Players.Grains.Messenger;

public interface IPlayerMessengerGrain : IGrainWithIntegerKey
{
    public Task<FriendListErrorCodeType> AddFriendAsync(
        PlayerSummarySnapshot snapshot,
        CancellationToken ct
    );
    public Task ForceAddFriendAsync(PlayerSummarySnapshot snapshot, CancellationToken ct);
    public Task<FriendListErrorCodeType> CanAddFriendAsync(PlayerId playerId, CancellationToken ct);
    public Task RemoveFriendsAsync(List<PlayerId> playerIds, CancellationToken ct);
    public Task ForceRemoveFriendAsync(PlayerId playerId, CancellationToken ct);
    public Task<List<MessengerAcceptFriendFailure>> AcceptFriendRequestsAsync(
        List<int> playerIds,
        CancellationToken ct
    );
    public Task DeclineFriendRequestsAsync(
        List<PlayerId> playerIds,
        bool declineAll,
        CancellationToken ct
    );
    public Task<MessengerRequestFriendResult> SendFriendRequestAsync(
        PlayerId playerId,
        CancellationToken ct
    );
    public Task<MessengerRequestFriendResult> ReceieveFriendRequestAsync(
        PlayerSummarySnapshot snapshot,
        CancellationToken ct
    );
    public Task BlockPlayerAsync(PlayerId targetId, CancellationToken ct);
    public Task UnblockPlayerAsync(PlayerId playerId, CancellationToken ct);
    public Task<MessengerIgnoreResultType> IgnorePlayerAsync(
        PlayerId targetId,
        CancellationToken ct
    );
    public Task<MessengerIgnoreResultType> UnignorePlayerAsync(
        PlayerId targetId,
        CancellationToken ct
    );
    public Task UpdateFriendsAsync(PlayerSummarySnapshot snapshot, CancellationToken ct);
    public Task RecieveFriendUpdateAsync(PlayerSummarySnapshot snapshot, CancellationToken ct);
    public Task<bool> SetRelationshipStatusAsync(
        PlayerId friendId,
        MessengerFriendRelationType status,
        CancellationToken ct
    );
    public Task<(
        List<MessengerSearchResultSnapshot> Friends,
        List<MessengerSearchResultSnapshot> Others
    )> SearchPlayersAsync(string query, CancellationToken ct);
    public Task<bool> SendMessageAsync(
        PlayerId recipientId,
        string message,
        int confirmationId,
        string senderName,
        string senderFigure,
        CancellationToken ct
    );
    public Task<bool> ReceiveMessageAsync(
        int chatId,
        string messageText,
        DateTime sentAtUtc,
        string messageId,
        int confirmationId,
        PlayerId senderId,
        string senderName,
        string senderFigure,
        int dbMessageId = 0
    );
    public Task FlushUpdatesAsync(CancellationToken ct);
    public Task<List<MessengerCategoryDto>> GetCategoriesAsync(CancellationToken ct);
    public Task<List<MessengerFriendDto>> GetFriendsAsync(CancellationToken ct);
    public Task<List<MessengerRequestDto>> GetRequestsAsync(CancellationToken ct);
    public Task<List<PlayerId>> GetIgnoredAsync(CancellationToken ct);
    public Task<List<MessengerUpdateSnapshot>> GetPendingUpdatesAsync(CancellationToken ct);
    public Task<List<RelationshipStatusEntrySnapshot>> GetRelationshipStatusInfoAsync(
        CancellationToken ct
    );
}
